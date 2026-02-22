using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Manages external events for thread-safe Revit API access.
    /// All Revit API calls must go through this manager since Revit is single-threaded.
    /// </summary>
    public class ExternalEventManager : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ConcurrentQueue<CommandRequest> _commandQueue = new ConcurrentQueue<CommandRequest>();

        public ExternalEventManager()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        /// <summary>
        /// Execute a command asynchronously through Revit's external event mechanism.
        /// Includes a 30-second timeout — if Revit doesn't process the event in time,
        /// the caller gets a timeout error instead of hanging forever.
        /// </summary>
        public async Task<JToken> ExecuteCommandAsync(string commandName, JObject parameters)
        {
            var tcs = new TaskCompletionSource<JToken>();
            var request = new CommandRequest
            {
                CommandName = commandName,
                Parameters = parameters,
                CompletionSource = tcs
            };

            _commandQueue.Enqueue(request);
            var raised = _externalEvent.Raise();
            
            if (raised != ExternalEventRequest.Accepted)
            {
                throw new InvalidOperationException(
                    $"Revit rejected the external event request (status: {raised}). " +
                    "Revit may be busy with another operation.");
            }

            // Timeout after 5 minutes — export operations can take significant time on large models
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            try
            {
                using (cts.Token.Register(() => tcs.TrySetException(
                    new TimeoutException($"Command '{commandName}' timed out after 5 minutes. Revit may be busy."))))
                {
                    return await tcs.Task;
                }
            }
            catch (AggregateException ae)
            {
                // Unwrap AggregateException from TaskCompletionSource
                throw ae.InnerException ?? ae;
            }
        }

        public void Execute(UIApplication app)
        {
            while (_commandQueue.TryDequeue(out var request))
            {
                try
                {
                    Application.ActiveUIApp = app;
                    var result = CommandExecutor.Execute(app, request.CommandName, request.Parameters);
                    request.CompletionSource.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Command '{request.CommandName}' failed in external event", ex);
                    request.CompletionSource.TrySetException(ex);
                }
            }
        }

        public string GetName() => "RevitMCPExternalEvent";

        private class CommandRequest
        {
            public string CommandName { get; set; } = "";
            public JObject Parameters { get; set; } = new JObject();
            public TaskCompletionSource<JToken> CompletionSource { get; set; } = null!;
        }
    }
}
