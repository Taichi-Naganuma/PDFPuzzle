using System.Windows;

namespace PDFPuzzle
{
    public partial class WorkflowSaveDialog : Window
    {
        public string WorkflowName { get; private set; } = string.Empty;
        public bool OverwriteConfirmed { get; private set; }

        public WorkflowSaveDialog(string suggestedName)
        {
            InitializeComponent();
            NameBox.Text = suggestedName;
            NameBox.SelectAll();
            NameBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusText.Text = LocalizationService.Get("Workflow_ErrorEmpty");
                return;
            }

            if (WorkflowService.Exists(name))
            {
                var result = MessageBox.Show(
                    LocalizationService.Get("Workflow_ConfirmOverwrite"),
                    LocalizationService.Get("Workflow_Save_Title"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.OK) return;
                OverwriteConfirmed = true;
            }

            WorkflowName = name;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
