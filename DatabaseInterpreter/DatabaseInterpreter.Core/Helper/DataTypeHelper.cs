﻿using DatabaseInterpreter.Model;
using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatabaseInterpreter.Core
{
    public class DataTypeHelper
    {
        public static readonly string[] CharTypeFlags = { "char" };
        public static readonly string[] TextTypeFlags = { "text" };
        public static readonly string[] BinaryTypeFlags = { "binary", "bytea" };
        public static readonly string[] DatetimeTypeFlags = { "date", "time" };
        public static readonly string[] GeometryTypeFlags = { "geometry", "geography"};
        public static List<string> SpecialDataTypes = new List<string>() { 
            nameof(SqlHierarchyId), nameof(SqlGeography), nameof(SqlGeometry),nameof(Geometry), "Byte[]"
        };

        public static bool IsCharType(string dataType)
        {
            return CharTypeFlags.Any(item => dataType.ToLower().Contains(item));
        }

        public static bool IsBinaryType(string dataType)
        {
            return BinaryTypeFlags.Any(item => dataType.ToLower().Contains(item));
        }

        public static bool IsGeometryType(string dataType)
        {
            return GeometryTypeFlags.Any(item => dataType.ToLower().Contains(item));
        }

        public static bool StartsWithN(string dataType)
        {
            return dataType.StartsWith("n", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTextType(string dataType)
        {
            return TextTypeFlags.Any(item => dataType.ToLower().Contains(item));
        }

        public static bool IsDatetimeType(string dataType)
        {
            return DatetimeTypeFlags.Any(item => dataType.ToLower().Contains(item));
        }

        public static bool IsUserDefinedType(TableColumn column)
        {
            string dataType = column.DataType;

            //although for its owned database, these are udt, but as a whole, they are not.
            if (dataType == "geography" || dataType == "geometry" || dataType == "st_geometry") 
            {
                return false;
            }

            return column.IsUserDefined;
        }

        public static DataTypeInfo GetDataTypeInfo(DbInterpreter dbInterpreter, string dataType)
        {
            DataTypeInfo dataTypeInfo = new DataTypeInfo();

            if (dbInterpreter != null)
            {
                if(!(dbInterpreter.DatabaseType== DatabaseType.Postgres && dataType== "\"char\""))
                {
                    dataType = dataType.Trim(dbInterpreter.QuotationLeftChar, dbInterpreter.QuotationRightChar);
                }               
            }

            int index = dataType.IndexOf("(");

            if (index > 0)
            {
                if (dataType.Substring(index + 1).IndexOf('(') > 0 || !dataType.Trim().EndsWith(")"))
                {
                    dataTypeInfo.DataType = dataType;
                }
                else
                {
                    dataTypeInfo.DataType = dataType.Substring(0, index);
                    dataTypeInfo.Args = dataType.Substring(index).Trim('(', ')').Trim();
                }
            }
            else
            {
                dataTypeInfo.DataType = dataType;
            }

            return dataTypeInfo;
        }

        public static DataTypeInfo GetDataTypeInfo(string dataType)
        {
            return GetDataTypeInfo(null, dataType);
        }

        public static DataTypeInfo GetSpecialDataTypeInfo(string dataType)
        {
            DataTypeInfo dataTypeInfo = new DataTypeInfo();

            Regex regex = new Regex("([(][0-9]+[)])");

            var matches = regex.Matches(dataType);

            List<string> args = new List<string>();

            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    dataType = dataType.Replace(match.Value, "");
                    args.Add(match.Value.Trim('(', ')'));
                }
            }

            dataTypeInfo.DataType = dataType;

            if (args.Count > 0)
            {
                dataTypeInfo.Args = string.Join(",", args);
            }

            return dataTypeInfo;
        }

        public static DataTypeInfo GetDataTypeInfoByTableColumn(TableColumn column)
        {
            return  new DataTypeInfo()
            {
                DataType = column.DataType,
                MaxLength = column.MaxLength,
                Precision = column.Precision,
                Scale = column.Scale,
                IsIdentity = column.IsIdentity
            };
        }  
        
        public static void SetDataTypeInfoToTableColumn(DataTypeInfo dataTypeInfo, TableColumn column)
        {
            column.DataType = dataTypeInfo.DataType;
            column.MaxLength = dataTypeInfo.MaxLength;
            column.Precision = dataTypeInfo.Precision;
            column.Scale = dataTypeInfo.Scale;
        }
    }
}
