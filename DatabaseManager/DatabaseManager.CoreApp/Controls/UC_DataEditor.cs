﻿using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DatabaseManager.Controls
{
    public partial class UC_DataEditor : UserControl, IDbObjContentDisplayer
    {
        private DatabaseObjectDisplayInfo displayInfo;
        private DbInterpreter dbInterpreter;
        private int sortedColumnIndex = -1;
        private SortOrder sortOrder = SortOrder.None;
        private bool isSorting = false;
        private QueryConditionBuilder conditionBuilder;
        private DataTable table;
        private List<TableColumn> columns;
        private List<TableColumn> identityColumns;
        private List<IndexColumn> pkColumns;
        private List<IndexColumn> uniqueIndexes;
        private const string GUID_ROW_NAME = "__row__guid__";


        public IEnumerable<DataGridViewColumn> Columns => this.dgvData.Columns.Cast<DataGridViewColumn>();
        public QueryConditionBuilder ConditionBuilder => this.conditionBuilder;
        public DataFilterHandler OnDataFilter;

        public UC_DataEditor()
        {
            InitializeComponent();

            this.cboAddMode.SelectedIndex = 2;

            this.pagination.PageSize = 50;

            this.dgvData.AutoGenerateColumns = true;

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, this.dgvData, new object[] { true });
        }

        public void Show(DatabaseObjectDisplayInfo displayInfo)
        {
            this.LoadData(displayInfo);
        }

        private async void LoadData(DatabaseObjectDisplayInfo displayInfo, long pageNum = 1, bool isSort = false)
        {
            this.displayInfo = displayInfo;

            this.pagination.PageNum = pageNum;

            DatabaseObject dbObject = displayInfo.DatabaseObject;

            int pageSize = this.pagination.PageSize;

            var option = new DbInterpreterOption() { ShowTextForGeometry = true };

            this.dbInterpreter = DbInterpreterHelper.GetDbInterpreter(displayInfo.DatabaseType, displayInfo.ConnectionInfo, option);

            SchemaInfoFilter filter = new SchemaInfoFilter() { Schema = dbObject.Schema, TableNames = [displayInfo.Name] };
            filter.DatabaseObjectType = DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey | DatabaseObjectType.Index;

            SchemaInfo schemaInfo = await this.dbInterpreter.GetSchemaInfoAsync(filter);

            this.columns = schemaInfo.TableColumns;

            var primaryKeys = schemaInfo.TablePrimaryKeys;
            this.pkColumns = primaryKeys.FirstOrDefault()?.Columns?.ToList();

            this.identityColumns = this.columns.Where(item => item.IsIdentity).ToList();
            this.uniqueIndexes = schemaInfo.TableIndexes.Where(item => item.IsUnique).SelectMany(item => item.Columns).ToList();

            string orderColumns = "";

            if (this.dgvData.SortedColumn != null)
            {
                string sortOrder = (this.sortOrder == SortOrder.Descending ? "DESC" : "ASC");
                orderColumns = $"{dbInterpreter.GetQuotedString(this.dgvData.SortedColumn.Name)} {sortOrder}";
            }

            string conditionClause = "";

            if (this.conditionBuilder != null && this.conditionBuilder.Conditions.Count > 0)
            {
                this.conditionBuilder.DatabaseType = dbInterpreter.DatabaseType;
                this.conditionBuilder.QuotationLeftChar = dbInterpreter.QuotationLeftChar;
                this.conditionBuilder.QuotationRightChar = dbInterpreter.QuotationRightChar;

                conditionClause = "WHERE " + this.conditionBuilder.ToString();
            }

            try
            {
                (long Total, DataTable Data) result = await dbInterpreter.GetPagedDataTableAsync(dbObject as Table, orderColumns, pageSize, pageNum, conditionClause, false);

                this.pagination.TotalCount = result.Total;

                this.table = DataGridViewHelper.ConvertDataTable(result.Data);

                this.AddIndentifierToDataTable(this.table);

                if (this.dgvData.Columns.Count == 0)
                {
                    this.AddColumns(this.table);
                }

                this.InsertData(this.table);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (this.sortedColumnIndex != -1)
            {
                DataGridViewColumn column = this.dgvData.Columns[this.sortedColumnIndex];

                this.isSorting = true;

                ListSortDirection sortDirection = this.GetSortDirection(this.sortOrder);

                this.dgvData.Sort(column, sortDirection);

                this.isSorting = false;
            }
        }

        private void AddIndentifierToDataTable(DataTable table)
        {
            table.Columns.Add(new DataColumn(GUID_ROW_NAME, typeof(Guid)));

            foreach (DataRow row in table.Rows)
            {
                row[GUID_ROW_NAME] = Guid.NewGuid();
            }
        }

        private void AddColumns(DataTable table)
        {
            foreach (DataColumn tc in table.Columns)
            {
                DataGridViewColumn column = new DataGridViewTextBoxColumn()
                {
                    Name = tc.ColumnName,
                    DataPropertyName = tc.ColumnName,
                    HeaderText = tc.ColumnName,
                    ValueType = tc.DataType,
                    Visible = tc.ColumnName != GUID_ROW_NAME,
                    ReadOnly = this.IsReadOnlyColumnByDataType(tc.DataType) || this.IsReadOnlyColumn(tc)
                };

                if (!this.CanSort(column))
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }

                this.dgvData.Columns.Add(column);
            }
        }

        private bool CanSort(DataGridViewColumn column)
        {
            if (column.ValueType == typeof(byte[]) || DataTypeHelper.IsGeometryType(column.ValueType.Name))
            {
                return false;
            }

            return true;
        }

        private bool IsReadOnlyColumn(DataColumn column)
        {
            foreach (var col in this.columns)
            {
                if (col.Name == column.ColumnName)
                {
                    if (col.IsComputed)
                    {
                        return true;
                    }
                    else if (col.IsIdentity && !this.dbInterpreter.CanInsertIdentityByDefault)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void InsertData(DataTable table)
        {
            this.dgvData.Rows.Clear();

            foreach (DataRow tr in table.Rows)
            {
                DataGridViewRow row = new DataGridViewRow();

                var values = tr.ItemArray;

                DataGridViewCell[] cells = new DataGridViewCell[table.Columns.Count];

                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new DataGridViewTextBoxCell();
                    cells[i].Value = values[i];
                }

                row.Cells.AddRange(cells);

                this.dgvData.Rows.Add(row);
            }
        }

        private bool IsReadOnlyColumnByDataType(Type dataType)
        {
            if (dataType == typeof(byte[]) || dataType == typeof(BitArray))
            {
                return true;
            }

            return false;
        }

        private ListSortDirection GetSortDirection(SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        }

        private void pagination_OnPageNumberChanged(long pageNum)
        {
            this.LoadData(this.displayInfo, pageNum);
        }

        public ContentSaveResult Save(ContentSaveInfo info)
        {
            DataTableHelper.WriteToFile(this.dgvData.DataSource as DataTable, info.FilePath);

            return new ContentSaveResult() { IsOK = true };
        }

        private void dgvData_Sorted(object sender, EventArgs e)
        {
            if (this.isSorting)
            {
                return;
            }

            this.sortedColumnIndex = this.dgvData.SortedColumn.DisplayIndex;
            this.sortOrder = this.dgvData.SortOrder;

            this.LoadData(this.displayInfo, 1, true);
        }

        private void btnFilter_Click(object sender, EventArgs e)
        {
            if (this.OnDataFilter != null)
            {
                this.OnDataFilter(this);
            }
        }

        public void FilterData(QueryConditionBuilder conditionBuilder)
        {
            this.conditionBuilder = conditionBuilder;

            this.LoadData(this.displayInfo, 1, false);
        }

        private void dgvData_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

        }

        private void dgvData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridViewHelper.FormatCell(this.dgvData, e);
        }

        private void dgvData_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var value = this.dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

            if (value != null)
            {
                if (value.GetType() != typeof(DBNull))
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        this.dgvData.CurrentCell = this.dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex];

                        this.SetContextMenuItemVisible();

                        this.cellContextMenu.Show(Cursor.Position);
                    }
                }
            }
        }

        private void SetContextMenuItemVisible()
        {
            this.tsmiViewGeometry.Visible = DataGridViewHelper.IsGeometryValue(this.dgvData);
        }

        private void tsmiCopy_Click(object sender, EventArgs e)
        {
            var value = DataGridViewHelper.GetCurrentCellValue(this.dgvData);

            if (!string.IsNullOrEmpty(value))
            {
                Clipboard.SetDataObject(value);
            }
        }

        private void tsmiViewGeometry_Click(object sender, EventArgs e)
        {
            DataGridViewHelper.ShowGeometryViewer(this.dgvData);
        }

        private void tsmiShowContent_Click(object sender, EventArgs e)
        {
            DataGridViewHelper.ShowCellContent(this.dgvData);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            this.AddRows(1);
        }

        private int GetRowGuidCoumnIndex()
        {
            for (int i = 0; i < this.dgvData.ColumnCount; i++)
            {
                if (this.dgvData.Columns[i].Name == GUID_ROW_NAME)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AddRows(int num)
        {
            int mode = this.cboAddMode.SelectedIndex;

            var selectedCells = this.dgvData.SelectedCells;

            int currentRowIndex = -1;

            if (selectedCells == null || selectedCells.Count == 0)
            {
                currentRowIndex = 0;
            }
            else
            {
                currentRowIndex = selectedCells.OfType<DataGridViewCell>().OrderBy(item => item.RowIndex).FirstOrDefault().RowIndex;
            }

            for (int i = 1; i <= num; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                DataGridViewTextBoxCell[] cells = new DataGridViewTextBoxCell[this.dgvData.ColumnCount];

                for (int j = 0; j < cells.Length; j++)
                {
                    cells[j] = new DataGridViewTextBoxCell();
                }

                int guidRowColumnIndex = this.GetRowGuidCoumnIndex();
                cells[guidRowColumnIndex].Value = Guid.NewGuid();

                row.Cells.AddRange(cells);

                if (mode == 2) // bottom
                {
                    this.dgvData.Rows.Add(row);
                }
                else if (mode == 0) //above
                {
                    this.dgvData.Rows.Insert(currentRowIndex++, row);
                }
                else if (mode == 1) //below
                {
                    this.dgvData.Rows.Insert(++currentRowIndex, row);
                }
            }

            this.SetControlState();
        }

        private void cboAddModes_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = this.cboAddMultipleRows.SelectedIndex;

            if (index == 0)
            {
                frmNumberSelector selector = new frmNumberSelector() { Text = "Insert multiple rows", Title = "Number of rows to insert.", MinValue = 1, MaxValue = 100 };

                var result = selector.ShowDialog();

                if (result == DialogResult.OK)
                {
                    int num = (int)selector.InputValue;

                    this.cboAddMultipleRows.SelectedIndex = -1;

                    this.AddRows(num);
                }
            }
        }

        private bool IsDataChanged()
        {
            for (int i = 0; i < this.dgvData.Rows.Count; i++)
            {
                var row = this.dgvData.Rows[i];
                var rowid = row.Cells[GUID_ROW_NAME].Value.ToString();

                var oldRow = this.GetOldRow(rowid);

                if (oldRow == null)
                {
                    if (!this.IsFullRowEmpty(row)) //added
                    {
                        return true;
                    }
                }
                else
                {
                    for (int j = 0; j < this.dgvData.ColumnCount; j++)
                    {
                        var value = row.Cells[j].Value;
                        var oldValue = oldRow[j];

                        if (value?.ToString() != oldValue?.ToString()) //updated
                        {
                            return true;
                        }
                    }
                }
            }

            foreach (DataRow row in this.table.Rows)
            {
                string rowid = row[GUID_ROW_NAME].ToString();

                var gridRow = this.GetGridViewRow(rowid);

                if (gridRow == null) // deleted
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFullRowEmpty(DataGridViewRow row)
        {
            int emptyValueCount = 0;

            for (int i = 0; i < this.dgvData.ColumnCount; i++)
            {
                string columnName = this.dgvData.Columns[i].Name;

                if (columnName != GUID_ROW_NAME)
                {
                    var value = row.Cells[i].Value;

                    if (value == null || value == DBNull.Value || value?.ToString() == string.Empty)
                    {
                        emptyValueCount++;
                    }
                }
            }

            return emptyValueCount == this.dgvData.ColumnCount - 1;
        }

        private bool ExistsEmptyRow()
        {
            foreach (DataGridViewRow row in this.dgvData.Rows)
            {
                if (this.IsFullRowEmpty(row))
                {
                    return true;
                }
            }

            return false;
        }

        private DataRow GetOldRow(string rowid)
        {
            foreach (DataRow row in this.table.Rows)
            {
                if (row[GUID_ROW_NAME]?.ToString() == rowid)
                {
                    return row;
                }
            }

            return null;
        }

        private DataGridViewRow GetGridViewRow(string rowid)
        {
            for (int i = 0; i < this.dgvData.Rows.Count; i++)
            {
                var row = this.dgvData.Rows[i];

                if (row.Cells[GUID_ROW_NAME]?.Value?.ToString() == rowid)
                {
                    return row;
                }
            }

            return null;
        }

        private void btnRevert_Click(object sender, EventArgs e)
        {
            bool needRevert = false;

            if (this.IsDataChanged())
            {
                var result = MessageBox.Show("Data has been changed, are you sure to revert it?", "Confirm", MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    needRevert = true;
                }
            }
            else
            {
                needRevert = this.ExistsEmptyRow();
            }

            if (needRevert)
            {
                this.InsertData(this.table);

                this.SetButtonEnabled(false);
                this.SetPaginationAndFilterEnabled(true);
            }
        }

        private bool IsCellAllowNull(int columnIndex)
        {
            DataColumn column = this.table.Columns[columnIndex];

            return column.AllowDBNull;
        }

        private bool IsNullValue(object value)
        {
            return value == null || value == DBNull.Value;
        }

        private void dgvData_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            int columnIndex = e.ColumnIndex;
            int rowIndex = e.RowIndex;

            if (columnIndex >= 0 && rowIndex >= 0)
            {
                if (this.dgvData.Columns[columnIndex].ReadOnly)
                {
                    return;
                }

                var cell = this.dgvData.Rows[rowIndex].Cells[columnIndex];

                if (!this.IsCellValid(cell))
                {
                    cell.ErrorText = "value can't be null";
                    return;
                }
                else
                {
                    cell.ErrorText = null;
                }
            }

            e.Cancel = false;
        }

        private TableColumn GetTableColumn(string columnName)
        {
            var column = this.columns.FirstOrDefault(item => item.Name == columnName);

            return column;
        }

        private DataColumn GetDataColumn(string columnName)
        {
            var column = this.table.Columns.OfType<DataColumn>().FirstOrDefault(item => item.ColumnName == columnName);

            return column;
        }

        private bool IsCellValid(DataGridViewCell cell)
        {
            var tableColumn = this.GetTableColumn(this.dgvData.Columns[cell.ColumnIndex].Name);

            if (!this.IsCellAllowNull(cell.ColumnIndex) && this.IsNullValue(cell.Value) && string.IsNullOrEmpty(tableColumn.DefaultValue))
            {
                return false;
            }

            return true;
        }

        private void dgvData_CellLeave(object sender, DataGridViewCellEventArgs e)
        {
            var cell = this.dgvData.CurrentCell;

            if (cell != null)
            {
                int columnIndex = cell.ColumnIndex;

                if (columnIndex >= 0 && this.dgvData.Columns[columnIndex].ReadOnly)
                {
                    return;
                }

                if (cell.IsInEditMode)
                {
                    if (cell.EditedFormattedValue != cell.Value)
                    {
                        cell.Value = cell.EditedFormattedValue;
                    }

                    if (cell.Value?.ToString() == string.Empty)
                    {
                        cell.Value = null;
                        this.dgvData.InvalidateCell(cell);
                    }

                    this.SetControlState();
                }
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var cells = this.dgvData.SelectedCells;

            List<int> rowIndexes = new List<int>();

            if (cells != null && cells.Count > 0)
            {
                foreach (DataGridViewCell cell in cells)
                {
                    int rowIndex = cell.RowIndex;

                    if (rowIndex >= 0)
                    {
                        rowIndexes.Add(rowIndex);
                    }
                }

                rowIndexes.OrderByDescending(item => item).ToList().ForEach(item => this.dgvData.Rows.RemoveAt(item));
            }

            this.SetControlState();
        }

        private void tsmiSetCellValueToNull_Click(object sender, EventArgs e)
        {
            var cell = this.dgvData.CurrentCell;

            if (cell != null)
            {
                cell.Value = DBNull.Value;
            }
        }

        private void SetButtonEnabled(bool enabled)
        {
            this.btnRevert.Enabled = enabled;
            this.btnCommit.Enabled = enabled;          
        }

        private void SetPaginationAndFilterEnabled(bool enabled)
        {
            this.pagination.Enabled = enabled;
            this.btnFilter.Enabled = enabled;
        }

        private void SetControlState()
        {
            bool changed = this.IsDataChanged();

            this.btnRevert.Enabled = changed || this.ExistsEmptyRow();
            this.btnCommit.Enabled = changed;
            this.pagination.Enabled = !changed;
            this.btnFilter.Enabled = !changed;

            if (changed)
            {
                foreach (DataGridViewColumn column in this.dgvData.Columns)
                {
                    var oldMode = column.SortMode;
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
            }
            else
            {
                foreach (DataGridViewColumn column in this.dgvData.Columns)
                {
                    if (!this.CanSort(column))
                    {
                        column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    }
                    else
                    {
                        column.SortMode = DataGridViewColumnSortMode.Automatic;
                    }
                }
            }
        }

        private bool IsDataGridViewValid()
        {
            for (int i = 0; i < this.dgvData.Rows.Count; i++)
            {
                var row = this.dgvData.Rows[i];

                if (this.IsFullRowEmpty(row))
                {
                    continue;
                }

                for (int j = 0; j < this.dgvData.ColumnCount; j++)
                {
                    var cell = row.Cells[j];

                    if (!this.IsCellValid(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void PasteClipboardValue()
        {
            var selectedCells = this.dgvData.SelectedCells;

            if (selectedCells.Count == 0)
            {
                return;
            }

            DataGridViewCell startCell = selectedCells.OfType<DataGridViewCell>().OrderBy(item => item.RowIndex).ThenBy(item => item.ColumnIndex).FirstOrDefault();

            Dictionary<int, Dictionary<int, string>> values = this.GetClipBoardValues(Clipboard.GetText());

            int rowIndex = startCell.RowIndex;

            foreach (int rowKey in values.Keys)
            {
                int columnIndex = startCell.ColumnIndex;

                foreach (int cellKey in values[rowKey].Keys)
                {
                    if (columnIndex <= this.dgvData.Columns.Count - 1 && rowIndex <= this.dgvData.Rows.Count - 1)
                    {
                        DataGridViewCell cell = this.dgvData[columnIndex, rowIndex];

                        cell.Value = values[rowKey][cellKey];
                    }

                    columnIndex++;
                }

                rowIndex++;
            }

            this.SetControlState();
        }

        private Dictionary<int, Dictionary<int, string>> GetClipBoardValues(string clipboardValue)
        {
            Dictionary<int, Dictionary<int, string>> copyValues = new Dictionary<int, Dictionary<int, string>>();

            string[] lines = clipboardValue.Split('\n');

            for (int i = 0; i <= lines.Length - 1; i++)
            {
                copyValues[i] = new Dictionary<int, string>();
                string[] lineContent = lines[i].Split('\t');

                if (lineContent.Length == 0)
                {
                    copyValues[i][0] = string.Empty;
                }
                else
                {
                    for (int j = 0; j <= lineContent.Length - 1; j++)
                    {
                        copyValues[i][j] = lineContent[j];
                    }
                }
            }

            return copyValues;
        }

        private void dgvData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                this.PasteClipboardValue();
            }
        }

        private void btnCommit_Click(object sender, EventArgs e)
        {
            this.dgvData.Invalidate();

            bool isValid = this.IsDataGridViewValid();

            if (!isValid)
            {
                MessageBox.Show("The grid data is invalid.");

                return;
            }

            this.SaveChanges();
        }

        private async void SaveChanges()
        {
            List<Dictionary<string, object>> insertData = new List<Dictionary<string, object>>();
            Dictionary<string, List<UpdateDataItemInfo>> updateData = new Dictionary<string, List<UpdateDataItemInfo>>();
            List<string> deleteData = new List<string>();

            for (int i = 0; i < this.dgvData.Rows.Count; i++)
            {
                var row = this.dgvData.Rows[i];
                var rowid = row.Cells[GUID_ROW_NAME].Value.ToString();

                var oldRow = this.GetOldRow(rowid);

                bool isNewRow = false;
                Dictionary<string, object> dictRow = new Dictionary<string, object>();
                List<UpdateDataItemInfo> updateDataItemInfos = new List<UpdateDataItemInfo>();

                if (oldRow == null) //added
                {
                    isNewRow = true;
                }

                for (int j = 0; j < this.dgvData.ColumnCount; j++)
                {
                    string columnName = this.dgvData.Columns[j].Name;
                    var value = row.Cells[j].Value;

                    if (isNewRow)
                    {
                        dictRow.Add(columnName, value);
                    }
                    else
                    {
                        if (this.IsReadOnlyColumn(this.GetDataColumn(columnName)))
                        {
                            continue;
                        }

                        var oldValue = oldRow[j];

                        if (value?.ToString() != oldValue?.ToString()) //updated
                        {
                            updateDataItemInfos.Add(new UpdateDataItemInfo() { ColumnName = columnName, OldValue = oldValue, NewValue = value });
                        }
                    }
                }

                if (isNewRow)
                {
                    insertData.Add(dictRow);
                }
                else
                {
                    if (updateDataItemInfos.Count > 0)
                    {
                        updateData.Add(rowid, updateDataItemInfos);
                    }
                }
            }

            foreach (DataRow row in this.table.Rows)
            {
                string rowid = row[GUID_ROW_NAME].ToString();

                var gridRow = this.GetGridViewRow(rowid);

                if (gridRow == null) // deleted
                {
                    deleteData.Add(rowid);
                }
            }

            this.dbInterpreter.Option.ScriptOutputMode = GenerateScriptOutputMode.WriteToString;

            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(this.dbInterpreter);

            (Table Table, List<TableColumn> Columns) tableAndColumns = (new Table() { Schema = this.displayInfo.Schema, Name = this.displayInfo.Name }, this.columns);

            (Dictionary<string, object> Parameters, string Script) insertScriptResult = this.GenerateScripts(scriptGenerator, tableAndColumns, insertData);

            string insertScript = insertScriptResult.Script;
            var updateScriptResult = this.GetUpdateScript(scriptGenerator, updateData);
            string deleteScript = this.GetDeleteScript(scriptGenerator, deleteData);

            try
            {
                using (DbConnection dbConnection = this.dbInterpreter.CreateConnection())
                {
                    if (dbConnection.State != ConnectionState.Open)
                    {
                        await dbConnection.OpenAsync();
                    }

                    var transaction = await dbConnection.BeginTransactionAsync();

                    await this.ExecuteScript(dbConnection, transaction, deleteScript, null);

                    await this.ExecuteScript(dbConnection, transaction, updateScriptResult.Script, updateScriptResult.Parameters);

                    await this.ExecuteScript(dbConnection, transaction, insertScript, insertScriptResult.Parameters);

                    transaction.Commit();

                    MessageBox.Show("Saved.");

                    this.ResetTableData();

                    this.SetButtonEnabled(false);
                    this.SetPaginationAndFilterEnabled(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ResetTableData()
        {
            this.table.Rows.Clear();

            for (int i = 0; i < this.dgvData.RowCount; i++)
            {
                DataRow dr = this.table.NewRow();

                for (int j = 0; j < this.dgvData.ColumnCount; j++)
                {
                    string columnName = this.dgvData.Columns[j].Name;
                    object value = this.dgvData.Rows[i].Cells[j].Value;

                    dr[columnName] = value;
                }

                this.table.Rows.Add(dr);
            }
        }

        private async Task ExecuteScript(DbConnection dbConnection, DbTransaction transaction, string script, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(script))
            {
                return;
            }

            string delimiter = ");" + Environment.NewLine;

            if (!script.Contains(delimiter))
            {
                await this.dbInterpreter.ExecuteNonQueryAsync(dbConnection, this.GetCommandInfo(script, parameters, transaction));
            }
            else
            {
                var items = script.Split(delimiter);

                int count = 0;

                foreach (var item in items)
                {
                    count++;

                    var cmd = count < items.Length ? (item + delimiter).Trim().Trim(';') : item;

                    await this.dbInterpreter.ExecuteNonQueryAsync(dbConnection, this.GetCommandInfo(cmd, parameters, transaction));
                }
            }
        }

        private CommandInfo GetCommandInfo(string commandText, Dictionary<string, object> parameters = null, DbTransaction transaction = null)
        {
            CommandInfo commandInfo = new CommandInfo()
            {
                CommandType = CommandType.Text,
                CommandText = commandText,
                Parameters = parameters,
                Transaction = transaction
            };

            return commandInfo;
        }

        private (Dictionary<string, object> Paramters, string Script) GenerateScripts(DbScriptGenerator scriptGenerator, (Table Table, List<TableColumn> Columns) targetTableAndColumns, List<Dictionary<string, object>> data)
        {
            StringBuilder sb = new StringBuilder();

            Dictionary<string, object> paramters = scriptGenerator.AppendDataScripts(sb, targetTableAndColumns.Table, targetTableAndColumns.Columns, new Dictionary<long, List<Dictionary<string, object>>>() { { 1, data } });

            string script = sb.ToString().Trim().Trim(';');

            return (paramters, script);
        }

        private (string Script, Dictionary<string, object> Parameters) GetUpdateScript(DbScriptGenerator scriptGenerator, Dictionary<string, List<UpdateDataItemInfo>> updateData)
        {
            List<string> scripts = new List<string>();
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            int i = 1;
            foreach (var kp in updateData)
            {
                string rowid = kp.Key;

                DataRow row = this.GetOldRow(rowid);

                if (row != null)
                {
                    string condition = this.GetDdlWhereCondition(scriptGenerator, row);

                    if (!string.IsNullOrEmpty(condition))
                    {
                        string tableName = this.dbInterpreter.GetQuotedDbObjectNameWithSchema(this.displayInfo.Schema, this.displayInfo.Name);

                        List<string> sets = new List<string>();

                        foreach (var info in kp.Value)
                        {
                            TableColumn tc = this.GetTableColumn(info.ColumnName);

                            object value = info.NewValue;

                            string parameterName = $"{this.dbInterpreter.CommandParameterChar}{info.ColumnName}{i}";

                            sets.Add($"{this.dbInterpreter.GetQuotedString(info.ColumnName)}={parameterName}");

                            parameters.Add(parameterName, value);
                        }

                        scripts.Add($"UPDATE {tableName} SET {string.Join(" ", sets)} WHERE {condition};");
                    }
                }
            }

            return (string.Join(Environment.NewLine, scripts), parameters);
        }

        private string GetDeleteScript(DbScriptGenerator scriptGenerator, List<string> deleteData)
        {
            List<string> scripts = new List<string>();

            foreach (string rowid in deleteData)
            {
                DataRow row = this.GetOldRow(rowid);

                if (row != null)
                {
                    string condition = this.GetDdlWhereCondition(scriptGenerator, row);

                    if (!string.IsNullOrEmpty(condition))
                    {
                        scripts.Add($"DELETE FROM {this.dbInterpreter.GetQuotedDbObjectNameWithSchema(this.displayInfo.Schema, this.displayInfo.Name)} WHERE {condition};");
                    }
                }
            }

            return string.Join(Environment.NewLine, scripts);
        }

        private string GetDdlWhereCondition(DbScriptGenerator scriptGenerator, DataRow row)
        {
            List<string> conditions = new List<string>();

            bool hasIdentity = this.identityColumns.Count > 0;

            foreach (DataColumn column in this.table.Columns)
            {
                string columnName = column.ColumnName;

                if (columnName == GUID_ROW_NAME)
                {
                    continue;
                }
              
                TableColumn tc = this.GetTableColumn(columnName);

                if(tc.IsComputed)
                {
                    continue;
                }

                var value = row[columnName];
                object parsedValue = scriptGenerator.ParseValue(tc, value, false);

                if (this.identityColumns.Any(item => item.Name == columnName))
                {
                    conditions.Add($"{columnName}={parsedValue}");
                    break;
                }

                if (!hasIdentity)
                {
                    bool needAdd = false;

                    if (this.pkColumns != null && this.pkColumns.Count > 0)
                    {
                        if (this.pkColumns.Any(item => item.ColumnName == columnName))
                        {
                            needAdd = true;
                        }
                    }
                    else if (this.uniqueIndexes != null && this.uniqueIndexes.Count > 0)
                    {
                        if (this.uniqueIndexes.Any(item => item.ColumnName == columnName))
                        {
                            needAdd = true;
                        }
                    }
                    else
                    {
                        needAdd = true;
                    }

                    if(needAdd)
                    {
                        conditions.Add(this.GetConditionItem(columnName, parsedValue));
                    }
                }
            }

            return string.Join(" AND ", conditions);
        }

        private string GetConditionItem(string columnName, object value)
        {
            return $"{this.dbInterpreter.GetQuotedString(columnName)} {(ValueHelper.IsNullValue(value)? " IS ":"=")} {value}";
        }

        internal class UpdateDataItemInfo
        {
            internal string ColumnName { get; set; }
            internal object OldValue { get; set; }
            internal object NewValue { get; set; }
        }
    }
}
