using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.Core;
using BIMBotPlugin.UI;
using WinForms = System.Windows.Forms;

namespace BIMBotPlugin.Commands
{
    /// <summary>
    /// "Join Bridge" ribbon button: joins the active Revit document to the BIM-Bot bridge as a slave of
    /// the Navisworks master (slot 01, 02, …), or leaves it when it already joined.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BridgeJoinCommand : IExternalCommand
    {
        private const string Title = "BIM-Bot Bridge";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument?.Document;
                if (doc == null || doc.IsFamilyDocument)
                {
                    TaskDialog.Show(Title, "Open the project model you want to link to Navisworks first.");
                    return Result.Cancelled;
                }

                var session = BridgeSessionFile.ReadLive();
                if (session == null)
                {
                    TaskDialog.Show(Title, "No bridge is running.\n\nIn Navisworks, open the federated model and click " +
                        "Add-ins > BIM-Bot Bridge (that Navisworks becomes the master). Then click Join Bridge here.");
                    return Result.Cancelled;
                }

                var docTitle = doc.Title;
                var mine = BridgeSessionFile.FindSlave(session, docTitle);
                if (mine != null)
                {
                    var leave = new TaskDialog(Title)
                    {
                        MainInstruction = "This model is Slave " + mine["slot"],
                        MainContent = "Linked to: " + mine["model"] + "\n\nLeave the bridge?",
                        CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                        DefaultButton = TaskDialogResult.No,
                    };
                    if (leave.Show() == TaskDialogResult.Yes)
                    {
                        BridgeSessionFile.Leave(session, docTitle);
                        TaskDialog.Show(Title, "'" + docTitle + "' left the bridge.");
                    }
                    return Result.Succeeded;
                }

                if (!Core.Application.IsServiceRunning) Core.Application.StartService(uiApp);
                var port = Core.Application.SocketServiceInstance?.Port ?? 0;
                if (port <= 0)
                {
                    TaskDialog.Show(Title, "The BIM-Bot service is not running in this Revit, so it cannot join. " +
                        "Click Start BIM-Bot and try again.");
                    return Result.Failed;
                }

                var models = BridgeSessionFile.Models(session);
                if (models.Count == 0)
                {
                    TaskDialog.Show(Title, "The Navisworks master lists no models. Restart the bridge in Navisworks.");
                    return Result.Failed;
                }

                using (var form = new BridgeModelPickerForm(docTitle, BridgeSessionFile.NextSlot(session), models,
                           BridgeSessionFile.BestMatch(docTitle, models)))
                {
                    if (form.ShowDialog() != WinForms.DialogResult.OK || form.SelectedModel == null) return Result.Cancelled;
                    var slot = BridgeSessionFile.Join(session, docTitle, doc.PathName ?? "", form.SelectedModel, port);
                    TaskDialog.Show(Title, "'" + docTitle + "' is now Slave " + slot + ".\nLinked to: " + form.SelectedModel +
                        "\n\nAsk Claude to take a Navisworks clash to Revit.");
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Bridge join failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
