using DatabaseManager.Core.Model;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DatabaseManager.Forms
{
    public partial class frmSqlQuery : Form
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SplitterDistance { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowEditorMessage { get; set; }

        public frmSqlQuery()
        {
            InitializeComponent();          
        }

        private void frmSqlQuery_Load(object sender, EventArgs e)
        {
            
        }

        public void Init()
        {
            this.ucSqlQuery.ReadOnly = this.ReadOnly;
            this.ucSqlQuery.ShowEditorMessage = this.ShowEditorMessage;
            this.ucSqlQuery.SplitterDistance = this.SplitterDistance;
        }

        public void Query(DatabaseObjectDisplayInfo displayInfo)
        {
            this.ucSqlQuery.Editor.Text = displayInfo.Content;           

            this.ucSqlQuery.RunScripts(displayInfo);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
