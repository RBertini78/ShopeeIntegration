namespace ShopeeIntegration
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblPartnerId;
        private System.Windows.Forms.TextBox txtPartnerId;
        private System.Windows.Forms.Label lblShopId;
        private System.Windows.Forms.TextBox txtShopId;
        private System.Windows.Forms.Label lblApiKey;
        private System.Windows.Forms.TextBox txtApiKey;
        private System.Windows.Forms.ComboBox cbEnvironment;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnListProducts;
        private System.Windows.Forms.Button btnUpdateSelected;
        private System.Windows.Forms.Button btnListOrders;
        private System.Windows.Forms.DataGridView dgvProducts;
        private System.Windows.Forms.DataGridView dgvOrders;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStatus;
        private System.Windows.Forms.Button btnGetAuthUrl;
        private System.Windows.Forms.TextBox txtAuthCode;
        private System.Windows.Forms.Button btnGetToken;
        private System.Windows.Forms.Label lblAuthCode;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            lblPartnerId = new System.Windows.Forms.Label();
            txtPartnerId = new System.Windows.Forms.TextBox();
            lblShopId = new System.Windows.Forms.Label();
            txtShopId = new System.Windows.Forms.TextBox();
            lblApiKey = new System.Windows.Forms.Label();
            txtApiKey = new System.Windows.Forms.TextBox();
            cbEnvironment = new System.Windows.Forms.ComboBox();
            btnConnect = new System.Windows.Forms.Button();
            btnListProducts = new System.Windows.Forms.Button();
            btnUpdateSelected = new System.Windows.Forms.Button();
            btnListOrders = new System.Windows.Forms.Button();
            dgvProducts = new System.Windows.Forms.DataGridView();
            dgvOrders = new System.Windows.Forms.DataGridView();
            statusStrip = new System.Windows.Forms.StatusStrip();
            toolStatus = new System.Windows.Forms.ToolStripStatusLabel();
            btnGetAuthUrl = new System.Windows.Forms.Button();
            txtAuthCode = new System.Windows.Forms.TextBox();
            btnGetToken = new System.Windows.Forms.Button();
            lblAuthCode = new System.Windows.Forms.Label();

            SuspendLayout();

            // Labels and textboxes for credentials
            lblPartnerId.AutoSize = true;
            lblPartnerId.Location = new System.Drawing.Point(12, 15);
            lblPartnerId.Name = "lblPartnerId";
            lblPartnerId.Size = new System.Drawing.Size(61, 15);
            lblPartnerId.Text = "Partner ID";

            txtPartnerId.Location = new System.Drawing.Point(85, 12);
            txtPartnerId.Name = "txtPartnerId";
            txtPartnerId.Size = new System.Drawing.Size(120, 23);

            lblShopId.AutoSize = true;
            lblShopId.Location = new System.Drawing.Point(215, 15);
            lblShopId.Name = "lblShopId";
            lblShopId.Size = new System.Drawing.Size(46, 15);
            lblShopId.Text = "Shop ID";

            txtShopId.Location = new System.Drawing.Point(265, 12);
            txtShopId.Name = "txtShopId";
            txtShopId.Size = new System.Drawing.Size(120, 23);

            lblApiKey.AutoSize = true;
            lblApiKey.Location = new System.Drawing.Point(395, 15);
            lblApiKey.Name = "lblApiKey";
            lblApiKey.Size = new System.Drawing.Size(45, 15);
            lblApiKey.Text = "API Key";

            txtApiKey.Location = new System.Drawing.Point(445, 12);
            txtApiKey.Name = "txtApiKey";
            txtApiKey.Size = new System.Drawing.Size(220, 23);

            cbEnvironment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cbEnvironment.Items.AddRange(new object[] { "Sandbox", "Production" });
            cbEnvironment.Location = new System.Drawing.Point(675, 12);
            cbEnvironment.Name = "cbEnvironment";
            cbEnvironment.Size = new System.Drawing.Size(110, 23);
            cbEnvironment.SelectedIndex = 0;

            btnConnect.Location = new System.Drawing.Point(12, 45);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new System.Drawing.Size(120, 27);
            btnConnect.Text = "Conectar";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += new System.EventHandler(this.BtnConnect_Click);

            btnListProducts.Location = new System.Drawing.Point(140, 45);
            btnListProducts.Name = "btnListProducts";
            btnListProducts.Size = new System.Drawing.Size(140, 27);
            btnListProducts.Text = "Listar Produtos";
            btnListProducts.UseVisualStyleBackColor = true;
            btnListProducts.Click += new System.EventHandler(this.BtnListProducts_Click);

            btnUpdateSelected.Location = new System.Drawing.Point(290, 45);
            btnUpdateSelected.Name = "btnUpdateSelected";
            btnUpdateSelected.Size = new System.Drawing.Size(170, 27);
            btnUpdateSelected.Text = "Atualizar Estoque/Preço";
            btnUpdateSelected.UseVisualStyleBackColor = true;
            btnUpdateSelected.Click += new System.EventHandler(this.BtnUpdateSelected_Click);

            btnListOrders.Location = new System.Drawing.Point(468, 45);
            btnListOrders.Name = "btnListOrders";
            btnListOrders.Size = new System.Drawing.Size(120, 27);
            btnListOrders.Text = "Listar Pedidos";
            btnListOrders.UseVisualStyleBackColor = true;
            btnListOrders.Click += new System.EventHandler(this.BtnListOrders_Click);

            // Authorization controls
            btnGetAuthUrl.Location = new System.Drawing.Point(595, 45);
            btnGetAuthUrl.Name = "btnGetAuthUrl";
            btnGetAuthUrl.Size = new System.Drawing.Size(110, 27);
            btnGetAuthUrl.Text = "Gerar Auth URL";
            btnGetAuthUrl.UseVisualStyleBackColor = true;
            btnGetAuthUrl.Click += new System.EventHandler(this.BtnGetAuthUrl_Click);

            lblAuthCode.AutoSize = true;
            lblAuthCode.Location = new System.Drawing.Point(12, 80);
            lblAuthCode.Name = "lblAuthCode";
            lblAuthCode.Size = new System.Drawing.Size(63, 15);
            lblAuthCode.Text = "Auth Code";

            txtAuthCode.Location = new System.Drawing.Point(85, 77);
            txtAuthCode.Name = "txtAuthCode";
            txtAuthCode.Size = new System.Drawing.Size(480, 23);

            btnGetToken.Location = new System.Drawing.Point(575, 75);
            btnGetToken.Name = "btnGetToken";
            btnGetToken.Size = new System.Drawing.Size(110, 27);
            btnGetToken.Text = "Trocar Token";
            btnGetToken.UseVisualStyleBackColor = true;
            btnGetToken.Click += new System.EventHandler(this.BtnGetToken_Click);

            // DataGridView Products
            dgvProducts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            dgvProducts.Location = new System.Drawing.Point(12, 110);
            dgvProducts.Name = "dgvProducts";
            dgvProducts.Size = new System.Drawing.Size(773, 200);
            dgvProducts.AllowUserToAddRows = false;
            dgvProducts.AllowUserToDeleteRows = false;
            dgvProducts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvProducts.AutoGenerateColumns = false;
            dgvProducts.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Item ID", DataPropertyName = "ItemId", ReadOnly = true });
            dgvProducts.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Nome", DataPropertyName = "Name", ReadOnly = true, Width = 300 });
            dgvProducts.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Estoque", DataPropertyName = "Stock" });
            dgvProducts.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Preço", DataPropertyName = "Price" });

            // DataGridView Orders
            dgvOrders.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            dgvOrders.Location = new System.Drawing.Point(12, 320);
            dgvOrders.Name = "dgvOrders";
            dgvOrders.Size = new System.Drawing.Size(773, 90);
            dgvOrders.AllowUserToAddRows = false;
            dgvOrders.AllowUserToDeleteRows = false;
            dgvOrders.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvOrders.AutoGenerateColumns = false;
            dgvOrders.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Pedido ID", DataPropertyName = "OrderId", ReadOnly = true });
            dgvOrders.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Comprador", DataPropertyName = "BuyerName", ReadOnly = true, Width = 300 });
            dgvOrders.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = "TotalPrice", ReadOnly = true });

            // Status strip
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStatus });
            statusStrip.Location = new System.Drawing.Point(0, 413);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(800, 25);

            toolStatus.Name = "toolStatus";
            toolStatus.Text = "Pronto";

            // Form
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 438);
            Controls.Add(lblPartnerId);
            Controls.Add(txtPartnerId);
            Controls.Add(lblShopId);
            Controls.Add(txtShopId);
            Controls.Add(lblApiKey);
            Controls.Add(txtApiKey);
            Controls.Add(cbEnvironment);
            Controls.Add(btnConnect);
            Controls.Add(btnListProducts);
            Controls.Add(btnUpdateSelected);
            Controls.Add(btnListOrders);
            Controls.Add(dgvProducts);
            Controls.Add(dgvOrders);
            Controls.Add(statusStrip);
            Controls.Add(btnGetAuthUrl);
            Controls.Add(lblAuthCode);
            Controls.Add(txtAuthCode);
            Controls.Add(btnGetToken);
            Name = "Form1";
            Text = "Shopee Integration";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
