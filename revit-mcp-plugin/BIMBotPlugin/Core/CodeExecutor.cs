using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Compiles and executes C# code at runtime against the Revit API using Roslyn.
    /// Works on .NET Framework 4.8 (Revit 2020-2024), .NET 8 (Revit 2025-2026), and .NET 10 (Revit 2027+).
    /// 
    /// The AI writes just the code body — we wrap it with:
    /// - Revit API imports (using statements)
    /// - A class + method wrapper
    /// - Access to Document, UIDocument, UIApplication via __doc__, __uidoc__, __uiapp__
    /// </summary>
    public static class CodeExecutor
    {
        private const int TimeoutSeconds = 30;

        /// <summary>
        /// Execute C# code against the Revit API.
        /// Parameters:
        ///   - code (string, required): C# code body to execute
        ///   - description (string, optional): What the code does (for logging)
        /// </summary>
        public static JToken Execute(UIApplication uiApp, JObject parameters)
        {
            var code = parameters["code"]?.ToString();
            var description = parameters["description"]?.ToString() ?? "AI-generated code";

            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("No code provided. The 'code' parameter is required.");

            var doc = uiApp.ActiveUIDocument?.Document;
            var uidoc = uiApp.ActiveUIDocument;

            if (doc == null)
                throw new InvalidOperationException("No active document. Please open a Revit project first.");

            try
            {
                // Step 1: Compile the code using Roslyn
                var assembly = CompileCode(code);

                // Step 2: Execute it with Revit context
                var result = ExecuteAssembly(assembly, uiApp, uidoc, doc);

                return new JObject
                {
                    ["success"] = true,
                    ["description"] = description,
                    ["result"] = result?.ToString() ?? "Code executed successfully (no return value)"
                };
            }
            catch (CompilationException ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = "Compilation failed",
                    ["errors"] = new JArray(ex.Errors.Select(e => e.ToString())),
                    ["hint"] = "Fix the compilation errors and try again. Common issues: missing 'using' statements, wrong types, or syntax errors. Note: In Revit 2025+, use new ElementId((long)value) instead of new ElementId((int)value)."
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Runtime error: {ex.Message}",
                    ["stackTrace"] = ex.StackTrace?.Split('\n').Take(5).ToArray() != null
                        ? string.Join("\n", ex.StackTrace.Split('\n').Take(5))
                        : "",
                    ["hint"] = "The code compiled but failed at runtime. Check element IDs, null references, and parameter names."
                };
            }
        }

        /// <summary>
        /// Wrap the AI's code body in a compilable class with all Revit imports,
        /// then compile using Roslyn (works on .NET 4.8, .NET 8, and .NET 10).
        /// </summary>
        private static Assembly CompileCode(string codeBody)
        {
            // Build the full source code with Revit API imports
            var fullSource = $@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

public class DynamicRevitCode
{{
    public object Run(UIApplication __uiapp__, UIDocument __uidoc__, Document __doc__)
    {{
        // Friendly aliases so AI/user code can use natural variable names
        var RevitApp = __uiapp__;
        var uiApp = __uiapp__;
        var app = __uiapp__.Application;
        var uidoc = __uidoc__;
        var doc = __doc__;

        {codeBody}
    }}
}}
";

            // Determine framework and Roslyn assembly paths
            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var csharpDllPath = Path.Combine(pluginDir, "Microsoft.CodeAnalysis.CSharp.dll");
            var coreDllPath = Path.Combine(pluginDir, "Microsoft.CodeAnalysis.dll");
            var collectionsDllPath = Path.Combine(pluginDir, "System.Collections.Immutable.dll");

            Assembly rCSharp = null;
            Assembly rCore = null;

#if NET5_0_OR_GREATER || NETCOREAPP
            // In .NET 8 (Revit 2025/2026), use a custom AssemblyLoadContext to heavily isolate Roslyn
            // Prevents Dynamo's Roslyn assemblies from causing MethodNotFound exceptions.
            var context = new System.Runtime.Loader.AssemblyLoadContext("BIMBotRoslyn隔離", isCollectible: true);
            try
            {
                if (File.Exists(collectionsDllPath)) context.LoadFromAssemblyPath(collectionsDllPath);
                if (File.Exists(coreDllPath)) rCore = context.LoadFromAssemblyPath(coreDllPath);
                if (File.Exists(csharpDllPath)) rCSharp = context.LoadFromAssemblyPath(csharpDllPath);
            }
            catch { }
#endif
            
            // Fallback for .NET 4.8 or if ALC loading fails
            if (rCSharp == null || rCore == null)
            {
                if (File.Exists(csharpDllPath)) rCSharp = Assembly.LoadFrom(csharpDllPath);
                else rCSharp = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Microsoft.CodeAnalysis.CSharp");

                if (File.Exists(coreDllPath)) rCore = Assembly.LoadFrom(coreDllPath);
                else rCore = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Microsoft.CodeAnalysis");
            }

            if (rCSharp == null || rCore == null)
                throw new Exception("Could not load Roslyn compiler assemblies (Microsoft.CodeAnalysis / Microsoft.CodeAnalysis.CSharp).");

            var csharpSyntaxTreeType = rCSharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
            var csharpCompilationOptionsType = rCSharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
            var csharpCompilationType = rCSharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
            var outputKindType = rCore.GetType("Microsoft.CodeAnalysis.OutputKind");
            var metadataReferenceType = rCore.GetType("Microsoft.CodeAnalysis.MetadataReference");
            var diagnosticSeverityType = rCore.GetType("Microsoft.CodeAnalysis.DiagnosticSeverity");
            var optimizationLevelType = rCore.GetType("Microsoft.CodeAnalysis.OptimizationLevel");

            // 1. CSharpSyntaxTree.ParseText(fullSource)
            var parseTextMethod = csharpSyntaxTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "ParseText" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));
            var parseArgs = BuildArgs(parseTextMethod.GetParameters(), fullSource);
            var syntaxTree = parseTextMethod.Invoke(null, parseArgs);

            // Create array of syntax trees
            var syntaxTreeArray = Array.CreateInstance(csharpSyntaxTreeType.BaseType ?? typeof(object), 1);
            syntaxTreeArray.SetValue(syntaxTree, 0);

            // 2. Build MetadataReferences natively using reflection
            var createFromFileMethod = metadataReferenceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "CreateFromFile" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));

            var referencesListType = typeof(List<>).MakeGenericType(metadataReferenceType);
            dynamic references = Activator.CreateInstance(referencesListType);

            var addedRefPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void SafeAddReference(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !addedRefPaths.Add(path)) return;
                try
                {
                    var args = BuildArgs(createFromFileMethod.GetParameters(), path);
                    var reference = createFromFileMethod.Invoke(null, args);
                    references.Add((dynamic)reference);
                }
                catch { }
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
                    SafeAddReference(asm.Location);
            }

            var revitDir = Path.GetDirectoryName(typeof(Autodesk.Revit.DB.Document).Assembly.Location);
            if (!string.IsNullOrEmpty(revitDir))
            {
                SafeAddReference(Path.Combine(revitDir, "RevitAPI.dll"));
                SafeAddReference(Path.Combine(revitDir, "RevitAPIUI.dll"));
            }

            // 3. CSharpCompilationOptions
            int outputKindDll = 2; // OutputKind.DynamicallyLinkedLibrary = 2
            if (outputKindType != null) outputKindDll = (int)Enum.Parse(outputKindType, "DynamicallyLinkedLibrary");

            var optionsCtor = csharpCompilationOptionsType.GetConstructors()
                .First(c => c.GetParameters().Length > 0 && c.GetParameters()[0].ParameterType.Name == "OutputKind");
            var optionsArgs = BuildArgs(optionsCtor.GetParameters(), Enum.ToObject(outputKindType, outputKindDll));
            var options = optionsCtor.Invoke(optionsArgs);

            var withOptMethod = csharpCompilationOptionsType.GetMethod("WithOptimizationLevel");
            if (withOptMethod != null && optimizationLevelType != null)
            {
                var optArgs = BuildArgs(withOptMethod.GetParameters(), Enum.ToObject(optimizationLevelType, 1)); // Release = 1
                options = withOptMethod.Invoke(options, optArgs);
            }

            // 4. CSharpCompilation.Create
            var createMethod = csharpCompilationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Create" && m.GetParameters().Length >= 4
                       && m.GetParameters()[0].ParameterType == typeof(string)
                       && m.GetParameters()[3].ParameterType == csharpCompilationOptionsType);

            var createArgs = BuildArgs(createMethod.GetParameters(), 
                $"DynamicCode_{Guid.NewGuid():N}", 
                syntaxTreeArray, 
                references, 
                options);

            dynamic compilation = createMethod.Invoke(null, createArgs);

            // 5. Emit
            using (var ms = new MemoryStream())
            {
                var emitResult = compilation.Emit(ms);
                bool isSuccess = emitResult.Success;

                if (!isSuccess)
                {
                    IEnumerable<dynamic> rawDiagnostics = emitResult.Diagnostics;
                    var errors = new List<string>();

                    foreach (var d in rawDiagnostics)
                    {
                        var severity = (int)d.Severity;
                        if (severity == 3) // Error = 3
                        {
                            var location = d.Location;
                            int line = 0;
                            try
                            {
                                var lineSpan = location.GetLineSpan();
                                line = lineSpan.StartLinePosition.Line - 20; // Offset wrapper
                            }
                            catch { }

                            errors.Add($"Line {line}: {d.GetMessage()}");
                        }
                    }
                    throw new CompilationException(errors);
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
        }

        /// <summary>
        /// Execute the compiled assembly directly.
        /// Code must handle its own Transactions if it modifies the document.
        /// </summary>
        private static object ExecuteAssembly(Assembly assembly, UIApplication uiApp, UIDocument uidoc, Document doc)
        {
            var type = assembly.GetType("DynamicRevitCode");
            if (type == null)
                throw new Exception("Failed to find the compiled code class.");

            var instance = Activator.CreateInstance(type);
            var method = type.GetMethod("Run");
            if (method == null)
                throw new Exception("Failed to find the Run method in compiled code.");

            object result = null;
            try
            {
                result = method.Invoke(instance, new object[] { uiApp, uidoc, doc });
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception for a cleaner error message
                throw ex.InnerException ?? ex;
            }

            // Convert result to a readable string
            if (result == null) return null;

            // Handle anonymous types and collections nicely
            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var items = new JArray();
                foreach (var item in enumerable)
                    items.Add(JToken.FromObject(item.ToString()));
                return items;
            }

            try
            {
                // Try JSON serialization for complex objects
                return JToken.FromObject(result);
            }
            catch
            {
                return result.ToString();
            }
        }

        /// <summary>
        /// Helper to build default args for a Method/Constructor with optional parameters at runtime.
        /// </summary>
        private static object[] BuildArgs(ParameterInfo[] parameters, params object[] knownArgs)
        {
            var args = new object[parameters.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (i < knownArgs.Length)
                {
                    args[i] = knownArgs[i];
                }
                else
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue :
                        (parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null);
                }
            }
            return args;
        }

        // Dangerous API patterns that should be flagged
        private static readonly string[] _blacklistedPatterns = new[]
        {
            "Process.Start", "File.Delete", "Directory.Delete",
            "Environment.Exit", "Registry.", "WebClient",
            "HttpClient", "System.Net.", "System.Diagnostics.Process"
        };

        /// <summary>
        /// Compile C# code without executing it — dry-run validation.
        /// Returns compilation success/errors and safety warnings.
        /// </summary>
        public static JToken Preview(UIApplication uiApp, JObject parameters)
        {
            var code = parameters["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("No code provided.");

            // Safety check
            var warnings = new JArray();
            foreach (var pattern in _blacklistedPatterns)
            {
                if (code.Contains(pattern))
                    warnings.Add($"⚠️ Potentially dangerous: code contains '{pattern}'");
            }

            try
            {
                CompileCode(code);
                return new JObject
                {
                    ["success"] = true,
                    ["message"] = "✅ Code compiles successfully. Safe to execute with send_code_to_revit.",
                    ["warnings"] = warnings,
                    ["warningCount"] = warnings.Count
                };
            }
            catch (CompilationException ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = "Compilation failed",
                    ["errors"] = new JArray(ex.Errors.Select(e => e.ToString())),
                    ["warnings"] = warnings,
                    ["hint"] = "Fix the errors and try preview_code again before executing."
                };
            }
        }
    }

    /// <summary>
    /// Exception for compilation errors with detailed error list.
    /// </summary>
    public class CompilationException : Exception
    {
        public List<string> Errors { get; }

        public CompilationException(List<string> errors)
            : base($"Compilation failed with {errors.Count} error(s)")
        {
            Errors = errors;
        }
    }
}
