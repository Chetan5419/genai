using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class ExecutionLogPage : Page
    {
        private ApiClient _apiClient;
        private ObservableCollection<ExecutionLog> _executionLogs;
        private ExecutionLog _selectedExecution;
        private TestPlan _currentTestPlan;

        public ExecutionLogPage()
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _executionLogs = new ObservableCollection<ExecutionLog>();
            ExecutionLogDataGrid.ItemsSource = _executionLogs;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProjectInfo();
            LoadExecutionLogs();
        }

        private void LoadProjectInfo()
        {
            if (Session.CurrentProject != null)
            {
                ProjectTitleTextBlock.Text = Session.CurrentProject.title;
                ProjectDetailsTextBlock.Text = $"Type: {Session.CurrentProject.projecttype}\nStarted: {Session.CurrentProject.startdate}";
            }
        }

        private async void LoadExecutionLogs()
        {
            try
            {
                StatusTextBlock.Text = "Loading execution history...";
                
                _apiClient.SetBearer(Session.Token);
                var response = await _apiClient.GetAsync("ExecutionLogs");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var logs = JsonSerializer.Deserialize<List<ExecutionLog>>(content, options);
                    
                    _executionLogs.Clear();
                    foreach (var log in logs.OrderByDescending(l => l.datestamp).ThenByDescending(l => l.exetime))
                    {
                        _executionLogs.Add(log);
                    }
                    
                    StatusTextBlock.Text = $"Loaded {logs.Count} execution records";
                }
                else
                {
                    StatusTextBlock.Text = $"Error loading logs: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }

        private void ExecutionLogDataGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ExecutionLogDataGrid.SelectedItem is ExecutionLog log)
            {
                _selectedExecution = log;
                GenerateScriptButton.IsEnabled = true;
                SelectedExecutionInfo.Text = $"Selected: {log.testcaseid} (Execution {log.exeid})";
            }
        }

        private async void GenerateScript_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedExecution == null)
            {
                MessageBox.Show("Please select an execution record first");
                return;
            }

            try
            {
                StatusTextBlock.Text = "Fetching test plan...";
                
                _apiClient.SetBearer(Session.Token);
                
                // Fetch test plan first
                var testPlanResponse = await _apiClient.GetAsync("TestPlan", new Dictionary<string, string>
                {
                    { "testCaseId", _selectedExecution.testcaseid }
                });

                if (!testPlanResponse.IsSuccessStatusCode)
                {
                    StatusTextBlock.Text = "Error fetching test plan";
                    return;
                }

                var testPlanContent = await testPlanResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _currentTestPlan = JsonSerializer.Deserialize<TestPlan>(testPlanContent, options);

                StatusTextBlock.Text = "Generating script...";

                // Generate script using POST with test plan in body
                var scriptPayload = new
                {
                    pretestid_steps = _currentTestPlan?.pretestid_steps ?? new Dictionary<string, Dictionary<string, string>>(),
                    pretestid_scripts = _currentTestPlan?.pretestid_scripts ?? new Dictionary<string, string>(),
                    current_testid = _selectedExecution.testcaseid,
                    current_bdd_steps = _currentTestPlan?.current_bdd_steps ?? new Dictionary<string, string>()
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(scriptPayload),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var scriptResponse = await _apiClient.PostAsync(
                    $"generate-test-script/{_selectedExecution.testcaseid}?script_type={_selectedExecution.scripttype}&script_lang=python",
                    jsonContent
                );

                if (scriptResponse.IsSuccessStatusCode)
                {
                    var scriptContent = await scriptResponse.Content.ReadAsStringAsync();
                    
                    // Show script result dialog
                    ShowScriptResultDialog(_selectedExecution.testcaseid, scriptContent, _selectedExecution.output);
                    StatusTextBlock.Text = "Script generated successfully";
                }
                else
                {
                    var errorContent = await scriptResponse.Content.ReadAsStringAsync();
                    StatusTextBlock.Text = $"Error generating script: {scriptResponse.StatusCode}";
                    MessageBox.Show($"Error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }

        private void ShowScriptResultDialog(string testCaseId, string script, string executionLog)
        {
            var window = new Window
            {
                Title = $"Generated Script - {testCaseId}",
                Width = 900,
                Height = 700,
                Background = System.Windows.Media.Brushes.Black,
                Foreground = System.Windows.Media.Brushes.White,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BackgroundBrush"] };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerBorder = new Border { Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["HeaderBrush"] };
            Grid.SetRow(headerBorder, 0);
            var headerText = new TextBlock 
            { 
                Text = $"Generated Playwright Script", 
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(20, 12, 20, 12)
            };
            headerBorder.Child = headerText;
            grid.Children.Add(headerBorder);

            // Script display
            var scriptBox = new TextBox
            {
                Text = script,
                IsReadOnly = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 20, 25)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 212, 255)),
                FontFamily = new System.Windows.Media.FontFamily("Courier New"),
                FontSize = 10,
                Padding = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scriptBox, 1);
            grid.Children.Add(scriptBox);

            // Execution log
            var logLabel = new TextBlock
            {
                Text = "Execution Log",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 212, 255)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(20, 12, 20, 8)
            };
            Grid.SetRow(logLabel, 1);
            grid.Children.Add(logLabel);

            var logBox = new TextBox
            {
                Text = executionLog,
                IsReadOnly = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 30, 39)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                FontFamily = new System.Windows.Media.FontFamily("Courier New"),
                FontSize = 9,
                Padding = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 150, 0, 0)
            };
            Grid.SetRow(logBox, 2);
            grid.Children.Add(logBox);

            // Footer with buttons
            var footerPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 12, 20, 12)
            };
            Grid.SetRow(footerPanel, 3);

            var downloadBtn = new Button
            {
                Content = "⬇ Download Script",
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 212, 255)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 20, 25)),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            downloadBtn.Click += (s, e) => DownloadScript(testCaseId, script);
            footerPanel.Children.Add(downloadBtn);

            var closeBtn = new Button
            {
                Content = "Close",
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)),
                Foreground = System.Windows.Media.Brushes.White,
                Padding = new Thickness(14, 8, 14, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            closeBtn.Click += (s, e) => window.Close();
            footerPanel.Children.Add(closeBtn);

            grid.Children.Add(footerPanel);

            window.Content = grid;
            window.ShowDialog();
        }

        private void DownloadScript(string testCaseId, string scriptContent)
        {
            try
            {
                var filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"{testCaseId}_generated_script_{DateTime.Now:yyyyMMdd_HHmmss}.py"
                );
                
                File.WriteAllText(filePath, scriptContent);
                StatusTextBlock.Text = $"Script downloaded to: {filePath}";
                MessageBox.Show($"Script saved to:\n{filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading script: {ex.Message}");
            }
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (Session.CurrentProject != null)
                NavigationService?.Navigate(new DashboardPage(Session.CurrentProject.projectid));
            else
                NavigationService?.Navigate(new ProjectPage());
        }

        private void AITestExecutor_Click(object sender, RoutedEventArgs e)
        {
            if (Session.CurrentProject != null)
                NavigationService?.Navigate(new AITestExecutorPage(Session.CurrentProject.projectid));
            else
                MessageBox.Show("Please select a project first");
        }

        private void ScriptGenerator_Click(object sender, RoutedEventArgs e) => 
            NavigationService?.Navigate(new ScriptGeneratorPage());

        private void UploadTestCase_Click(object sender, RoutedEventArgs e) => 
            NavigationService?.Navigate(new UploadTestCasePage());

        private void ChangeProject_Click(object sender, RoutedEventArgs e) => 
            NavigationService?.Navigate(new ProjectPage());
    }
}
