using System;
using System.Drawing;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Views
{
    using TextBox = System.Windows.Forms.TextBox;
    using Button = System.Windows.Forms.Button;
    using Label = System.Windows.Forms.Label;
    using Cursor = System.Windows.Forms.Cursor;
    using Cursors = System.Windows.Forms.Cursors;
    using Panel = System.Windows.Forms.Panel;
    using Padding = System.Windows.Forms.Padding;
    using ContentAlignment = System.Drawing.ContentAlignment;
    using FontStyle = System.Drawing.FontStyle;
    using FormStartPosition = System.Windows.Forms.FormStartPosition;
    using FormBorderStyle = System.Windows.Forms.FormBorderStyle;
    using DialogResult = System.Windows.Forms.DialogResult;

    public class ConnectDBForm : Form
    {
        private TextBox txtServer, txtPort, txtDatabase, txtUser, txtPassword;
        private Button btnTest, btnConnect;
        private Label lblStatus, lblTitle;
        private Panel pnlCard;
        private bool isTested = false;

        public ConnectDBForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Form Settings
            this.Text = "Database Connection Setup";
            this.Size = new Size(450, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(245, 246, 250); // Light Gray Background
            this.Font = new Font("Segoe UI", 10);

            // Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(47, 54, 64) // Dark Navy
            };

            lblTitle = new Label
            {
                Text = "DATABASE CONNECTION",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            pnlHeader.Controls.Add(lblTitle);

            // Main Card Panel
            pnlCard = new Panel
            {
                Size = new Size(380, 380),
                Location = new Point(35, 100),
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            // Simple Shadow effect
            this.Paint += (s, e) => {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(200, 200, 200)), pnlCard.Left + 5, pnlCard.Top + 5, pnlCard.Width, pnlCard.Height);
            };

            int labelX = 20, inputX = 140, startY = 30, gap = 45;

            // Inputs
            AddInput(pnlCard, "Server IP:", out txtServer, labelX, startY);
            AddInput(pnlCard, "Port:", out txtPort, labelX, startY + gap);
            txtPort.Text = "1433";
            AddInput(pnlCard, "Database:", out txtDatabase, labelX, startY + (gap * 2));
            AddInput(pnlCard, "Username:", out txtUser, labelX, startY + (gap * 3));
            AddInput(pnlCard, "Password:", out txtPassword, labelX, startY + (gap * 4), true);

            // Status Label
            lblStatus = new Label
            {
                Text = "Ready to connect",
                Location = new Point(20, startY + (gap * 5)),
                Size = new Size(340, 25),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            pnlCard.Controls.Add(lblStatus);

            // Buttons
            btnTest = new Button
            {
                Text = "Test Connection",
                Location = new Point(35, 490),
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(0, 120, 215), // Blue
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnTest.FlatAppearance.BorderSize = 0;
            btnTest.Click += BtnTest_Click;

            btnConnect = new Button
            {
                Text = "Connect",
                Location = new Point(235, 490),
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(40, 167, 69), // Green
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false, // Disabled until tested
                Cursor = Cursors.Hand
            };
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlCard);
            this.Controls.Add(btnTest);
            this.Controls.Add(btnConnect);
        }

        private void AddInput(Panel parent, string labelText, out TextBox tb, int x, int y, bool isPassword = false)
        {
            Label lbl = new Label { Text = labelText, Location = new Point(x, y), AutoSize = true, ForeColor = Color.FromArgb(64, 64, 64) };
            tb = new TextBox { Location = new Point(140, y - 3), Width = 200, BorderStyle = BorderStyle.FixedSingle };
            if (isPassword) tb.PasswordChar = '*';
            parent.Controls.Add(lbl);
            parent.Controls.Add(tb);
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            btnTest.Enabled = false;
            btnTest.Text = "Testing...";
            lblStatus.Text = "⏳ Validating connection...";
            lblStatus.ForeColor = Color.Orange;
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            string connStr = $"Server={txtServer.Text},{txtPort.Text};Database={txtDatabase.Text};User Id={txtUser.Text};Password={txtPassword.Text};";
            
            bool success = await Task.Run(() => {
                try {
                    using (SqlConnection conn = new SqlConnection(connStr)) {
                        conn.Open();
                        return true;
                    }
                } catch { return false; }
            });

            this.Cursor = System.Windows.Forms.Cursors.Default;
            btnTest.Enabled = true;
            btnTest.Text = "Test Connection";

            if (success) {
                lblStatus.Text = "✔ Connected Successfully";
                lblStatus.ForeColor = Color.Green;
                btnConnect.Enabled = true;
                isTested = true;
            } else {
                lblStatus.Text = "❌ Connection Failed";
                lblStatus.ForeColor = Color.Red;
                btnConnect.Enabled = false;
                isTested = false;
            }
        }

        public string GetConnectionString()
        {
            return $"Server={txtServer.Text},{txtPort.Text};Database={txtDatabase.Text};User Id={txtUser.Text};Password={txtPassword.Text};";
        }
    }
}
