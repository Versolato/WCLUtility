using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Negri.Wcl
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainForm));

        public MainForm()
        {
            InitializeComponent();
            Log.Info("MainForm open");
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                textBoxStatus.Text = "Select the Battlefy Master File";
                var res = openFileDialog.ShowDialog();
                if (res != DialogResult.OK)
                {
                    textBoxStatus.Text = "No file was selected.";
                    return;
                }
                var originalFile = openFileDialog.FileName;

                Cursor.Current = Cursors.WaitCursor;
                textBoxStatus.Text = "Validating... please wait.";
                buttonStart.Enabled = false;

                WclValidator validator = new WclValidator()
                {
                    AppId = GetApplicationId()
                };
                validator.Run(originalFile);
                textBoxStatus.Text = $"Validation done. Result on {validator.ResultFile}";

            }
            catch(Exception ex)
            {                
                Log.Error("buttonStart_Click", ex);
                MessageBox.Show(ex.Message, "General Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
                buttonStart.Enabled = true;
            }
        }

        private static string GetApplicationId()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(directory))
            {
                Log.Warn("You are using the 'demo' API Key. The application may not work at all! (Not founf the assembly directory)");
                return "demo";
            }

            var file = Path.Combine(directory, "AppId.txt");
            if (!File.Exists(file))
            {
                Log.Warn($"You are using the 'demo' API Key. The application may not work at all! (File '{file}' does not exists)");
                return "demo";
            }

            return File.ReadAllText(file, Encoding.UTF8).Trim();
        }

        private void buttonShowLog_Click(object sender, EventArgs e)
        {
            try
            {
                var appender = LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a.Name == "RollingFileAppender");
                if (appender == null)
                {
                    MessageBox.Show(@"There is no appender called 'RollingFileAppender' in log4net.");
                    return;
                }

                if (!(appender is log4net.Appender.FileAppender fa))
                {
                    MessageBox.Show(@"The appender 'RollingFileAppender' is not a child of FileAppender.");
                    return;
                }
                string file = fa.File ?? string.Empty;
                if (!File.Exists(file))
                {
                    MessageBox.Show($@"The log file '{file}' does not exists.");
                    return;
                }

                System.Diagnostics.Process.Start(file);
            }
            catch (Exception ex)
            {
                Log.Error("buttonShowLog_Click", ex);
                MessageBox.Show(ex.Message, "General Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
