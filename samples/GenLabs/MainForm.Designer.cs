public partial class Form1 : System.Windows.Forms.Form
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (this.components != null))
        {
            this.components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.pictureBox = new System.Windows.Forms.PictureBox();
        this.timer1 = new System.Windows.Forms.Timer(this.components);
        this.groupBox = new System.Windows.Forms.GroupBox();
        this.YPos = new System.Windows.Forms.TextBox();
        this.label5 = new System.Windows.Forms.Label();
        this.XPos = new System.Windows.Forms.TextBox();
        this.label4 = new System.Windows.Forms.Label();
        this.label2 = new System.Windows.Forms.Label();
        this.label6 = new System.Windows.Forms.Label();
        this.FastGenerate = new System.Windows.Forms.CheckBox();
        this.Size = new System.Windows.Forms.TextBox();
        this.label3 = new System.Windows.Forms.Label();
        this.label1 = new System.Windows.Forms.Label();
        this.button1 = new System.Windows.Forms.Button();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
        this.groupBox.SuspendLayout();
        this.SuspendLayout();
        // 
        // pictureBox
        // 
        this.pictureBox.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
        this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pictureBox.Location = new System.Drawing.Point(0, 0);
        this.pictureBox.Name = "pictureBox";
        this.pictureBox.Size = new System.Drawing.Size(594, 505);
        this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
        this.pictureBox.TabIndex = 0;
        this.pictureBox.TabStop = false;
        // 
        // timer1
        // 
        this.timer1.Interval = 1;
        this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
        // 
        // groupBox
        // 
        this.groupBox.Controls.Add(this.YPos);
        this.groupBox.Controls.Add(this.label5);
        this.groupBox.Controls.Add(this.XPos);
        this.groupBox.Controls.Add(this.label4);
        this.groupBox.Controls.Add(this.label2);
        this.groupBox.Controls.Add(this.label6);
        this.groupBox.Controls.Add(this.FastGenerate);
        this.groupBox.Controls.Add(this.Size);
        this.groupBox.Controls.Add(this.label3);
        this.groupBox.Controls.Add(this.label1);
        this.groupBox.Controls.Add(this.button1);
        this.groupBox.Location = new System.Drawing.Point(141, 137);
        this.groupBox.Name = "groupBox";
        this.groupBox.Size = new System.Drawing.Size(249, 184);
        this.groupBox.TabIndex = 1;
        this.groupBox.TabStop = false;
        this.groupBox.Text = "Settings";
        // this.groupBox.Enter += new System.EventHandler(this.GroupBox_Enter);
        // 
        // YPos
        // 
        this.YPos.Location = new System.Drawing.Point(161, 52);
        this.YPos.Name = "YPos";
        this.YPos.Size = new System.Drawing.Size(33, 20);
        this.YPos.TabIndex = 15;
        this.YPos.Text = "0";
        // 
        // label5
        // 
        this.label5.AutoSize = true;
        this.label5.Location = new System.Drawing.Point(138, 55);
        this.label5.Name = "label5";
        this.label5.Size = new System.Drawing.Size(17, 13);
        this.label5.TabIndex = 14;
        this.label5.Text = "Y:";
        // 
        // XPos
        // 
        this.XPos.Location = new System.Drawing.Point(91, 52);
        this.XPos.Name = "XPos";
        this.XPos.Size = new System.Drawing.Size(33, 20);
        this.XPos.TabIndex = 13;
        this.XPos.Text = "0";
        // 
        // label4
        // 
        this.label4.AutoSize = true;
        this.label4.Location = new System.Drawing.Point(68, 55);
        this.label4.Name = "label4";
        this.label4.Size = new System.Drawing.Size(17, 13);
        this.label4.TabIndex = 12;
        this.label4.Text = "X:";
        // 
        // label2
        // 
        this.label2.AutoSize = true;
        this.label2.Location = new System.Drawing.Point(6, 55);
        this.label2.Name = "label2";
        this.label2.Size = new System.Drawing.Size(56, 13);
        this.label2.TabIndex = 11;
        this.label2.Text = "Start Pos: ";
        // 
        // label6
        // 
        this.label6.AutoSize = true;
        this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
        this.label6.Location = new System.Drawing.Point(6, 118);
        this.label6.Name = "label6";
        this.label6.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
        this.label6.Size = new System.Drawing.Size(233, 13);
        this.label6.TabIndex = 10;
        this.label6.Text = "With fast generation, you will have to wait a little";
        // 
        // FastGenerate
        // 
        this.FastGenerate.AutoSize = true;
        this.FastGenerate.Location = new System.Drawing.Point(98, 90);
        this.FastGenerate.Name = "FastGenerate";
        this.FastGenerate.Size = new System.Drawing.Size(15, 14);
        this.FastGenerate.TabIndex = 9;
        this.FastGenerate.UseVisualStyleBackColor = true;
        // 
        // Size
        // 
        this.Size.Location = new System.Drawing.Point(69, 17);
        this.Size.Name = "Size";
        this.Size.Size = new System.Drawing.Size(167, 20);
        this.Size.TabIndex = 4;
        this.Size.Text = "10";
        // 
        // label3
        // 
        this.label3.AutoSize = true;
        this.label3.Location = new System.Drawing.Point(6, 91);
        this.label3.Name = "label3";
        this.label3.Size = new System.Drawing.Size(85, 13);
        this.label3.TabIndex = 3;
        this.label3.Text = "Fast Generation:";
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(6, 25);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(56, 13);
        this.label1.TabIndex = 1;
        this.label1.Text = "Rect Size:";
        // 
        // button1
        // 
        this.button1.Location = new System.Drawing.Point(161, 144);
        this.button1.Name = "button1";
        this.button1.Size = new System.Drawing.Size(75, 23);
        this.button1.TabIndex = 0;
        this.button1.Text = "Apply";
        this.button1.UseVisualStyleBackColor = true;
        this.button1.Click += new System.EventHandler(this.button1_Click);
        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.AutoSize = true;
        this.ClientSize = new System.Drawing.Size(594, 505);
        this.Controls.Add(this.groupBox);
        this.Controls.Add(this.pictureBox);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        this.Name = "Form1";
        this.Text = "Form1";
        this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        this.Load += new System.EventHandler(this.Form1_Load);
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
        this.groupBox.ResumeLayout(false);
        this.groupBox.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.PictureBox pictureBox;
    private System.Windows.Forms.Timer timer1;
    private System.Windows.Forms.GroupBox groupBox;
    private System.Windows.Forms.TextBox YPos;
    private System.Windows.Forms.Label label5;
    private System.Windows.Forms.TextBox XPos;
    private System.Windows.Forms.Label label4;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.Label label6;
    private System.Windows.Forms.CheckBox FastGenerate;
    private System.Windows.Forms.TextBox Size;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.Button button1;
}
