﻿using System.Collections.Generic;

namespace SqlAnalyser.Model
{
    public class SelectStatement : Statement
    {
        public List<ColumnName> Columns { get; set; } = new List<ColumnName>();
        public TokenInfo IntoTableName { get; set; }
        public TableName TableName { get; set; }
        public TokenInfo Where { get; set; }
        public List<TokenInfo> GroupBy { get; set; }
        public TokenInfo Having { get; set; }
        public List<UnionStatement> UnionStatements { get; set; }
        public List<WithStatement> WithStatements { get; set; }
        public List<FromItem> FromItems { get; set; }
        public List<TokenInfo> OrderBy { get; set; }
        public TokenInfo Option { get; set; }
        public SelectTopInfo TopInfo { get; set; }
        public SelectLimitInfo LimitInfo { get; set; }
    }

    public class SelectTopInfo
    {
        public TokenInfo TopCount { get; set; }
        public bool IsPercent { get; set; }
    }

    public class SelectLimitInfo
    {
        public TokenInfo StartRowIndex { get; set; }
        public TokenInfo RowCount { get; set; }
    }

    public class FromItem
    {
        public TableName TableName { get; set; }
        public TokenInfo Alias { get; set; }
        public SelectStatement SubSelectStatement { get; set; }
        public List<JoinItem> JoinItems { get; set; } = new List<JoinItem>();
    }

    public class JoinItem
    {
        public JoinType Type { get; set; }
        public TableName TableName { get; set; }
        public TokenInfo Condition { get; set; }
        public PivotItem PivotItem { get; set; }
        public UnPivotItem UnPivotItem { get; set; }
        public TokenInfo Alias { get; set; }
    }

    public class PivotItem : StatementItem
    {
        public TokenInfo AggregationFunctionName { get; set; }
        public TokenInfo AggregatedColumnName { get; set; }
        public ColumnName ColumnName { get; set; }
        public List<TokenInfo> Values { get; set; } = new List<TokenInfo>();
    }

    public class UnPivotItem : StatementItem
    {
        public ColumnName ValueColumnName { get; set; }
        public ColumnName ForColumnName { get; set; }
        public List<ColumnName> InColumnNames { get; set; } = new List<ColumnName>();
    }

    public enum JoinType
    {
        INNER = 0,
        LEFT = 1,
        RIGHT = 2,
        FULL = 3,
        CROSS = 4,
        PIVOT = 5,
        UNPIVOT = 6
    }
}
