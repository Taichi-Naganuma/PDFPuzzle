using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace PDFPuzzle
{
    public partial class WorkflowLoadDialog : Window
    {
        public class WorkflowRow
        {
            public WorkflowDto Source { get; set; } = new();
            public string Name => Source.Name;
            public string StepSummary { get; set; } = string.Empty;
            public string CreatedAtDisplay => Source.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        }

        public WorkflowDto? SelectedWorkflow { get; private set; }
        private ObservableCollection<WorkflowRow> _rows = new();

        public WorkflowLoadDialog()
        {
            InitializeComponent();
            Reload();
        }

        private void Reload()
        {
            _rows.Clear();
            foreach (var wf in WorkflowService.List())
            {
                var summary = string.Join(" → ", wf.StepKeys
                    .Select(k => LocalizationService.Get(k)));
                _rows.Add(new WorkflowRow { Source = wf, StepSummary = summary });
            }
            WorkflowList.ItemsSource = _rows;
            EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e) => TryLoadSelected();

        private void WorkflowList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryLoadSelected();

        private void TryLoadSelected()
        {
            if (WorkflowList.SelectedItem is not WorkflowRow row)
            {
                StatusText.Text = LocalizationService.Get("Workflow_Load_NoSelection");
                return;
            }
            SelectedWorkflow = row.Source;
            DialogResult = true;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowList.SelectedItem is not WorkflowRow row)
            {
                StatusText.Text = LocalizationService.Get("Workflow_Load_NoSelection");
                return;
            }

            var result = MessageBox.Show(
                string.Format(LocalizationService.Get("Workflow_ConfirmDelete"), row.Name),
                LocalizationService.Get("Workflow_Delete"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            WorkflowService.Delete(row.Name);
            Reload();
            StatusText.Text = string.Empty;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
