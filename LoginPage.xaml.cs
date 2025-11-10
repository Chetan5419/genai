using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new ApiClient();
                var login = new LoginCreate { username = UsernameTextBox.Text, password = PasswordBox.Password };
                var content = new StringContent(JsonSerializer.Serialize(login), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Login", content);
                if (response.IsSuccessStatusCode)
                {
                    var resp = JsonSerializer.Deserialize<LoginResponse>(await response.Content.ReadAsStringAsync());
                    Session.CurrentUser = resp;
                    Session.Token = resp.token;
                    client.SetBearer(resp.token);
                    this.NavigationService.Navigate(new ProjectPage());
                }
                else
                {
                    MessageBox.Show("Login failed: " + response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new RegisterPage());
        }
    }
}
