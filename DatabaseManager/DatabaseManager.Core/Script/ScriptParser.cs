﻿using DatabaseInterpreter.Core;
using DatabaseInterpreter.Utility;
using DatabaseManager.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DatabaseManager.Core
{
    public class ScriptParser
    {
        private readonly string selectPattern = "SELECT(.[\n]?)+(FROM)?";
        private readonly string dmlPattern = @"\b(CREATE|ALTER|INSERT|UPDATE|DELETE|TRUNCATE|INTO)\b";
        private readonly string createAlterScriptPattern = @"\b(CREATE|ALTER).+(VIEW|FUNCTION|PROCEDURE|TRIGGER)\b";
        private readonly string routinePattern = @"\b(BEGIN|END|DECLARE|SET|GOTO)\b";
        private DbInterpreter dbInterpreter;
        private string originalScript;
        private string cleanScript;

        public const string AsPattern = @"\b(AS|IS)\b";

        public string Script => this.originalScript;
        public string CleanScript => this.cleanScript;

        public ScriptParser(DbInterpreter dbInterpreter, string script)
        {
            this.dbInterpreter = dbInterpreter;
            this.originalScript = script;

            this.Parse();
        }

        private string Parse()
        {
            StringBuilder sb = new StringBuilder();

            string[] lines = this.originalScript.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Trim().StartsWith(this.dbInterpreter.CommentString))
                {
                    continue;
                }

                sb.AppendLine(line);
            }

            this.cleanScript = sb.ToString();

            return this.cleanScript;
        }

        public bool IsSelect()
        {
            if (Regex.IsMatch(this.cleanScript, selectPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)
                && !Regex.IsMatch(this.originalScript, dmlPattern, RegexOptions.IgnoreCase)
                && !Regex.IsMatch(this.originalScript, routinePattern, RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        public bool IsCreateOrAlterScript()
        {
            return Regex.IsMatch(this.cleanScript, this.createAlterScriptPattern);
        }

        public static ScriptType DetectScriptType(string script, DbInterpreter dbInterpreter)
        {
            string upperScript = script.ToUpper().Trim();

            ScriptParser scriptParser = new ScriptParser(dbInterpreter, upperScript);

            if (scriptParser.IsCreateOrAlterScript())
            {
                string firstLine = upperScript.Split(Environment.NewLine).FirstOrDefault();

                var asMatch = Regex.Match(firstLine, AsPattern);

                int asIndex = asMatch.Index;

                if (asIndex <= 0)
                {
                    asIndex = firstLine.Length;
                }

                string prefix = upperScript.Substring(0, asIndex);

                if (prefix.IndexOf(" VIEW ") > 0)
                {
                    return ScriptType.View;
                }
                else if (prefix.IndexOf(" FUNCTION ") > 0)
                {
                    return ScriptType.Function;
                }
                else if (prefix.IndexOf(" PROCEDURE ") > 0 || prefix.IndexOf(" PROC ") > 0)
                {
                    return ScriptType.Procedure;
                }
                else if (prefix.IndexOf(" TRIGGER ") > 0)
                {
                    return ScriptType.Trigger;
                }
            }
            else if (scriptParser.IsSelect())
            {
                return ScriptType.SimpleSelect;
            }

            return ScriptType.Other;
        }

        public static ScriptContentInfo GetContentInfo(string script, string lineSeperator, string commentString)
        {
            int lineSeperatorLength = lineSeperator.Length;

            ScriptContentInfo info = new ScriptContentInfo();

            string[] lines = script.Split(lineSeperator);

            int count = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                TextLineInfo lineInfo = new TextLineInfo() { Index = i, FirstCharIndex = count };

                if (lines[i].Trim().StartsWith(commentString) || lines[i].Trim().StartsWith("**"))
                {
                    lineInfo.Type = TextLineType.Comment;
                }

                if (i < lines.Length - 1)
                {
                    lineInfo.Length = lines[i].Length + lineSeperatorLength;
                }
                else
                {
                    lineInfo.Length = script.EndsWith(lineSeperator) ? lines[i].Length + lineSeperatorLength : lines[i].Length;
                }

                count += lines[i].Length + lineSeperatorLength;

                info.Lines.Add(lineInfo);
            }

            return info;
        }
    }

    public enum ScriptType
    {
        SimpleSelect = 1,
        View = 2,
        Function = 3,
        Procedure = 4,
        Trigger = 5,
        Other = 6
    }
}
