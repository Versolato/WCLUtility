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
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace Negri.Wcl
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainForm));

        private Timer _statusTimer;

        public MainForm()
        {
            InitializeComponent();
            Log.Info("MainForm open");

            _statusTimer = new Timer(500);
            _statusTimer.Elapsed += OnUpdateStatus;
            _statusTimer.AutoReset = true;             
        }

        private WclValidator _validator = null;

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (_validator != null)
                {
                    textBoxStatus.Text = "Already stated!";
                    return;
                }

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
                progressBar.Value = 0;
                buttonStart.Enabled = false;

                _validator = new WclValidator()
                {
                    AppId = GetApplicationId()
                };

                _statusTimer.Start();

                Task.Factory.StartNew(() => _validator.Run(originalFile)).ContinueWith(t => 
                {
                    _statusTimer.Stop();
                    Cursor.Current = Cursors.Default;
                    buttonStart.Invoke(new MethodInvoker(delegate { buttonStart.Enabled = true; }));
                    SetStatus($"Done! {_validator.ValidRecords:N0} of {_validator.TotalRecords:N0} records are 100% valid.");
                    progressBar.Invoke(new MethodInvoker(delegate { progressBar.Value = 1000; }));
                    _validator = null;
                });
                                
            }
            catch(Exception ex)
            {                
                Log.Error(nameof(ButtonStart_Click), ex);
                MessageBox.Show(ex.Message, "General Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void ButtonShowLog_Click(object sender, EventArgs e)
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
                Log.Error(nameof(ButtonShowLog_Click), ex);
                MessageBox.Show(ex.Message, "General Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUpdateStatus(object sender, ElapsedEventArgs e)
        {
            try
            {
                var v = _validator;
                if (v == null)
                {
                    _statusTimer.Stop();
                    return;
                }

                SetStatus(v.Status);
                progressBar.Invoke(new MethodInvoker(delegate { progressBar.Value = (int)(1000.0 * v.Progress); }));
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                Log.Error("OnUpdateStatus", ex);
            }
            finally
            {                
            }
        }

        private void SetStatus(string msg)
        {
            Log.Info($"Status: {msg}");
            textBoxStatus.Invoke(new MethodInvoker(delegate { textBoxStatus.Text = msg; }));
        }

        private void ButtonDeleteCache_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("Delete all cached queries on the WG API?", "Delete Cache Files", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes)
            {
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                var di = new DirectoryInfo(Path.GetTempPath());
                foreach(var fi in di.EnumerateFiles("wcl.*.json"))
                {
                    fi.Delete();
                }
            }
            catch(Exception ex)
            {
                Log.Error(nameof(ButtonDeleteCache_Click), ex);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }
    }
}
