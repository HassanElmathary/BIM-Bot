using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BIMBotPlugin.UI
{
    /// <summary>Small dialog: which model of the Navisworks master is this Revit document?</summary>
    public class BridgeModelPickerForm : Form
    {
        private readonly ComboBox _models = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 560,
        };

        public string? SelectedModel => _models.SelectedItem as string;

        public BridgeModelPickerForm(string documentTitle, string slotPreview, IList<string> models, string? preselect)
        {
            Text = "BIM-Bot Bridge — Join as slave";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Text = "Revit document:  " + documentTitle + "\n" +
                       "Joins as:  Slave " + slotPreview + "\n\n" +
                       "Which model in the Navisworks master is this document?",
            };

            foreach (var m in models) _models.Items.Add(m);
            if (preselect != null && _models.Items.Contains(preselect)) _models.SelectedItem = preselect;
            else if (_models.Items.Count > 0) _models.SelectedIndex = 0;

            var ok = new Button { Text = "Join", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(label);
            layout.Controls.Add(_models);
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
