﻿using DatabaseConverter.Core;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace DatabaseManager
{
    public partial class frmSetting : Form
    {
        private List<string> convertConcatCharTargetDatabases;

        public frmSetting()
        {
            InitializeComponent();

            this.Init();
        }

        private void frmSetting_Load(object sender, EventArgs e)
        {
            this.splitContainer1.Panel2.AutoScrollMinSize = new Size(400, 100);
        }

        private async void Init()
        {
            this.tabControl1.SelectedIndex = 0;

            var dbObjectNameModes = Enum.GetNames(typeof(DbObjectNameMode));
            this.cboDbObjectNameMode.Items.AddRange(dbObjectNameModes);

            Setting setting = SettingManager.Setting;

            this.numCommandTimeout.Value = setting.CommandTimeout;
            this.numDataBatchSize.Value = setting.DataBatchSize;
            this.chkShowBuiltinDatabase.Checked = setting.ShowBuiltinDatabase;
            this.chkUseOriginalDataTypeIfUdtHasOnlyOneAttr.Checked = setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr;
            this.txtMySqlCharset.Text = setting.MySqlCharset;
            this.txtMySqlCharsetCollation.Text = setting.MySqlCharsetCollation;
            this.chkNotCreateIfExists.Checked = setting.NotCreateIfExists;
            this.chkEnableLog.Checked = setting.EnableLog;
            this.cboDbObjectNameMode.Text = setting.DbObjectNameMode.ToString();
            this.chkLogInfo.Checked = setting.LogType.HasFlag(LogType.Info);
            this.chkLogError.Checked = setting.LogType.HasFlag(LogType.Error);
            this.chkEnableEditorHighlighting.Checked = setting.EnableEditorHighlighting;
            this.chkEditorEnableIntellisence.Checked = setting.EnableEditorIntellisence;
            this.chkExcludePostgresExtensionObjects.Checked = setting.ExcludePostgresExtensionObjects;
            this.chkValidateScriptsAfterTranslated.Checked = setting.ValidateScriptsAfterTranslated;
            this.chkShowTextEditorLineNumber.Checked = setting.TextEditorOption.ShowLineNumber;
            this.AddFonts(this.cboTextEditorFontName);
            this.cboTextEditorFontName.Text = setting.TextEditorOption.FontName;
            this.numTextEditorFontSize.Value = (decimal)setting.TextEditorOption.FontSize;

            var dbTypes = Enum.GetNames(typeof(DatabaseType));
            this.cboPreferredDatabase.Items.AddRange(dbTypes);
            this.chkRememberPasswordDuringSession.Checked = setting.RememberPasswordDuringSession;
            this.cboPreferredDatabase.Text = setting.PreferredDatabase.ToString();
            this.txtScriptOutputFolder.Text = setting.ScriptsDefaultOutputFolder;

            if(string.IsNullOrEmpty(setting.CustomMappingFolder))
            {
                this.txtCustomMappingFolder.Text = DataTypeMappingManager.CustomConfigRootFolder;
            }
            else
            {
                this.txtCustomMappingFolder.Text = setting.CustomMappingFolder;
            }           

            var themeTypes = Enum.GetNames(typeof(ThemeType));
            this.cboThemeType.Items.AddRange(themeTypes);
            this.cboThemeType.Text = setting.ThemeOption.ThemeType.ToString();

            PersonalSetting ps = await PersonalSettingManager.GetPersonalSetting();

            if (ps != null && !string.IsNullOrEmpty(ps.LockPassword))
            {
                this.txtLockPassword.Text = ps.LockPassword;
            }

            this.convertConcatCharTargetDatabases = setting.ConvertConcatCharTargetDatabases;

            this.lvOption.Items[0].Selected = true;
        }

        private void AddFonts(ComboBox comboBox)
        {
            InstalledFontCollection fonts = new InstalledFontCollection();

            foreach (FontFamily font in fonts.Families)
            {
                comboBox.Items.Add(font.Name);
            }
        }

        private async void btnConfirm_Click(object sender, EventArgs e)
        {
            Setting setting = SettingManager.Setting;
            setting.CommandTimeout = (int)this.numCommandTimeout.Value;
            setting.DataBatchSize = (int)this.numDataBatchSize.Value;
            setting.ShowBuiltinDatabase = this.chkShowBuiltinDatabase.Checked;
            setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr = this.chkUseOriginalDataTypeIfUdtHasOnlyOneAttr.Checked;
            setting.MySqlCharset = this.txtMySqlCharset.Text.Trim();
            setting.MySqlCharsetCollation = this.txtMySqlCharsetCollation.Text.Trim();
            setting.NotCreateIfExists = this.chkNotCreateIfExists.Checked;
            setting.EnableLog = this.chkEnableLog.Checked;
            setting.DbObjectNameMode = (DbObjectNameMode)Enum.Parse(typeof(DbObjectNameMode), this.cboDbObjectNameMode.Text);
            setting.RememberPasswordDuringSession = this.chkRememberPasswordDuringSession.Checked;
            setting.EnableEditorHighlighting = this.chkEnableEditorHighlighting.Checked;
            setting.EnableEditorIntellisence = this.chkEditorEnableIntellisence.Checked;
            setting.ExcludePostgresExtensionObjects = this.chkExcludePostgresExtensionObjects.Checked;
            setting.ScriptsDefaultOutputFolder = this.txtScriptOutputFolder.Text;
            setting.ValidateScriptsAfterTranslated = this.chkValidateScriptsAfterTranslated.Checked;
            setting.CustomMappingFolder = this.txtCustomMappingFolder.Text;

            string password = this.txtLockPassword.Text.Trim();

            PersonalSetting ps = new PersonalSetting() { LockPassword = password };

            await PersonalSettingManager.Save(ps);

            if (this.cboPreferredDatabase.SelectedIndex >= 0)
            {
                setting.PreferredDatabase = (DatabaseType)Enum.Parse(typeof(DatabaseType), this.cboPreferredDatabase.Text);
            }

            LogType logType = LogType.None;

            if (this.chkLogInfo.Checked)
            {
                logType |= LogType.Info;
            }

            if (this.chkLogError.Checked)
            {
                logType |= LogType.Error;
            }

            setting.LogType = logType;

            setting.ConvertConcatCharTargetDatabases = this.convertConcatCharTargetDatabases;

            TextEditorOption textEditorOption = new TextEditorOption()
            {
                ShowLineNumber = this.chkShowTextEditorLineNumber.Checked,
                FontName = this.cboTextEditorFontName.Text,
                FontSize = (float)this.numTextEditorFontSize.Value
            };

            setting.TextEditorOption = textEditorOption;

            ThemeOption themeOption = new ThemeOption() { ThemeType = (ThemeType)Enum.Parse(typeof(ThemeType), this.cboThemeType.Text) };

            setting.ThemeOption = themeOption;

            SettingManager.SaveConfig(setting);

            DbInterpreter.Setting = SettingManager.GetInterpreterSetting();
        }

        private void btnScriptOutputFolder_Click(object sender, EventArgs e)
        {
            if (this.dlgOutputFolder == null)
            {
                this.dlgOutputFolder = new FolderBrowserDialog();
            }

            DialogResult result = this.dlgOutputFolder.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.txtScriptOutputFolder.Text = this.dlgOutputFolder.SelectedPath;
            }
        }

        private void btnSelectTargetDatabaseTypesForConcatChar_Click(object sender, EventArgs e)
        {
            frmItemsSelector selector = new frmItemsSelector("Select Database Types", ItemsSelectorHelper.GetDatabaseTypeItems(this.convertConcatCharTargetDatabases));

            if (selector.ShowDialog() == DialogResult.OK)
            {
                this.convertConcatCharTargetDatabases = selector.CheckedItem.Select(item => item.Name).ToList();
            }
        }

        private void lvOption_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.lvOption.SelectedItems.Count > 0)
            {
                string option = this.lvOption.SelectedItems[0].Text;

                this.SetPanelVisible(option);
            }
        }

        private void SetPanelVisible(string option)
        {
            var controls = this.splitContainer1.Panel2.Controls;

            foreach (var control in controls)
            {
                if (control is Panel panel)
                {
                    string name = panel.Name;

                    if (name.EndsWith($"_{option}"))
                    {
                        panel.Visible = true;
                        panel.Top = 0;
                    }
                    else
                    {
                        panel.Visible = false;
                    }
                }
            }
        }

        private void btnSetDataTypeMapping_Click(object sender, EventArgs e)
        {
            frmDataTypeMappingSetting form = new frmDataTypeMappingSetting();

            form.ShowDialog();
        }

        private void btnCustomFolderMapping_Click(object sender, EventArgs e)
        {
            if (this.dlgOutputFolder == null)
            {
                this.dlgOutputFolder = new FolderBrowserDialog();
            }

            DialogResult result = this.dlgOutputFolder.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.txtCustomMappingFolder.Text = this.dlgOutputFolder.SelectedPath;
            }
        }
    }
}
