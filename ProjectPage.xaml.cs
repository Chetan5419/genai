using System.Windows;
using System.Windows.Controls;

namespace jpmc_genai
{
    public partial class ProjectPage : Page
    {
        public ProjectPage()
        {
            InitializeComponent();
            DataContext = new { Projects = Session.CurrentUser?.projects };
        }

        private void ProjectCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string projectId)
            {
                var selectedProject = Session.CurrentUser?.projects?.Find(p => p.projectid == projectId);
                if (selectedProject != null)
                {
                    Session.CurrentProject = selectedProject;
                }
                this.NavigationService.Navigate(new DashboardPage(projectId));
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            Session.CurrentUser = null;
            Session.Token = null;
            Session.CurrentProject = null;
            this.NavigationService.Navigate(new LoginPage());
        }
    }
}
