﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOwnLanguageNEW
{
    static class Utilities
    {
        /// <summary>
        /// Parse the string code to a runtime value.
        /// </summary>
        /// <param name="line">The line where text is located.</param>
        /// <param name="strText">The string text to parse.</param>
        /// <returns></returns>
        public static dynamic GetValue(int line, string strText, bool parseVar = true)
        {
            // Si el valor empieza con comillas:
            if (strText.StartsWith("\"") && strText.EndsWith("\""))
            {
                return strText.Trim('"');
            }

            // Si no contiene comillas
            if (!strText.Contains("\""))
            {
                // Si termina con f:
                if (strText.EndsWith("f"))
                {
                    float floatValue;
                    if (float.TryParse(strText.Trim('f'), out floatValue))
                    {
                        return floatValue;
                    }
                }
                if (int.TryParse(strText, out int intValue)) // Si NO tiene f y PUEDE se int:
                {
                    return intValue;
                }
            }

            // Si está escrito como true/True o false/False:
            if (strText.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                strText.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return bool.Parse(strText);
            }

            // Si no, buscar el valor de una variable con dicho nombre si es que existe:
            Variable variable = Init.runtimeVariables.FirstOrDefault(v => v.name == strText);
            if (variable == null && parseVar)
            {
                string[] all = strText.Split('.');
                variable = Init.runtimeVariables.FirstOrDefault(v => v.name == all[0]);
                if (variable != null)
                {
                    if (all[1] == "type") { return variable.GetValueType(); }
                    if (int.TryParse(all[1], out int index)) { return variable.GetValueAtIndex(line, index); }
                }
            }
            if (variable != null && parseVar)
            {
                return variable.value;
            }

            // Si no, buscar si es un comando aparte
            foreach (Libraries.Library library in Init.activeLibraries)
            {
                int firstParentesis = strText.IndexOf('(');
                int secondParentesis = strText.IndexOf(')');
                List<string> commandToExecute = new List<string>() { strText };
                if (firstParentesis != -1 && secondParentesis != -1)
                {
                    commandToExecute.Clear();
                    commandToExecute.Add(strText.Substring(0, firstParentesis));
                    string parameters = strText.Substring(firstParentesis + 1, secondParentesis - firstParentesis - 1);
                    commandToExecute.AddRange(parameters.Split(' '));
                }
                dynamic value = library.ExecuteCommand(commandToExecute.ToArray(), line);
                if (value != null)
                {
                    return value;
                }
            }

            // Si no, lanzar un error y devolver null:
            //ExceptionManager.UnknowType(line, strText);
            return strText;
        }

        public static bool EvaluateIfConditions(int line, string[] command)
        {
            List<string[]> allConditions = new List<string[]>();
            List<string> tempList = new List<string>();
            foreach (string s in command.Skip(1))
            {
                if (s != "and" && s != "or")
                {
                    tempList.Add(s);
                }
                else
                {
                    allConditions.Add(tempList.ToArray());
                    allConditions.Add(new string[] { s });
                    tempList.Clear();
                }
            }
            allConditions.Add(tempList.ToArray());
            tempList.Clear();

            bool result = ConcatenateValues(allConditions[0], line);
            for (int i = 1; i < allConditions.Count; i += 2)
            {
                string logicalOperator = allConditions[i][0];
                try
                {
                    string[] nextCondition = allConditions[i + 1];
                    if (logicalOperator == "and") { result = result && ConcatenateValues(nextCondition, line); }
                    if (logicalOperator == "or") { result = result || ConcatenateValues(nextCondition, line); }
                }
                catch { ExceptionManager.SyntaxError(line, "If <conditions>"); return false; }
            }
            return result;
        }

        public static dynamic ConcatenateValues(string[] values, int line, bool parseVar = true)
        {
            dynamic result = GetValue(line, values[0], parseVar);
            if (values.Length == 1) { return result; }

            // Check if every value ends with a ',' to create a list.
            if (values[0].EndsWith(","))
            {
                List<dynamic> list = new List<dynamic>();
                for (int i = 0; i < values.Length; i++)
                {
                    // Remove the ',' from the value, except from the last one.
                    string valueToAdd = i != values.Length - 1 ? values[i].Substring(0, values[i].Length - 1) : values[i];
                    if (values[i].EndsWith(",") || i == values.Length - 1) { list.Add(GetValue(line, valueToAdd, parseVar)); }
                    // This else only apply if the value isn't the last one.
                    else if (i != values.Length - 1) { ExceptionManager.InvalidValueOnListCreation(line, values[i]); return null; }
                }
                result = list;
                return result;
            }

            for (int i = 1; i < values.Length; i += 2)
            {
                string op = values[i];
                dynamic value = GetValue(line, values[i + 1], parseVar);

                try
                {
                    switch (op)
                    {
                        case "+":
                            result += value;
                            break;
                        case "-":
                            result -= value;
                            break;
                        case "/":
                            result /= value;
                            break;
                        case "*":
                            result *= value;
                            break;
                        case "==":
                            result = result == value;
                            break;
                        case "!=":
                            result = result != value;
                            break;
                        default:
                            ExceptionManager.UnknowOperation(line, op);
                            break;
                    }
                }
                catch { ExceptionManager.OperationNOTAllowed(line, op); }
            }

            return result;
        }

        public static int GetParametersNumber(string[] command, int line)
        {
            List<string[]> parameters = new List<string[]>();
            List<string> actualParameter = new List<string>();

            for (int i = 0; i < command.Length; i++)
            {
                if (command[i] != "|")
                {
                    actualParameter.Add(command[i]);
                }
                else
                {
                    parameters.Add(actualParameter.ToArray());
                    actualParameter.Clear();
                }
            }
            if (parameters.Count > 0) { parameters.Add(actualParameter.ToArray()); }
            // Si no hay ningun parametro hasta ahora entonces no hay divisiores "|":
            if (parameters.Count == 0)
            {
                bool modify = false;
                foreach (string s in command) { parameters.Add(new string[] { s }); }
                foreach (string[] array in parameters)
                {
                    if (array[0] == "+" || array[0] == "-" || array[0] == "/" || array[0] == "*" || array[0] == "==" || array[0] == "!=")
                    {
                        modify = true;
                    }
                }
                if (modify)
                {
                    List<string> temp = new List<string>();
                    foreach (string[] parm in parameters) { temp.Add(parm[0]); }
                    parameters.Clear();
                    parameters.Add(temp.ToArray());
                }
            }

            return parameters.Count;
        }

        public static dynamic GetCommandParameter(string[] command, int parameter, int line, bool parseVar = true)
        {
            List<string[]> parameters = new List<string[]>();
            List<string> actualParameter = new List<string>();

            for (int i = 0; i < command.Length; i++)
            {
                if (command[i] != "|")
                {
                    actualParameter.Add(command[i]);
                }
                else
                {
                    parameters.Add(actualParameter.ToArray());
                    actualParameter.Clear();
                }
            }
            if (parameters.Count > 0) { parameters.Add(actualParameter.ToArray()); }
            // Si no hay ningun parametro hasta ahora entonces no hay divisiores "|":
            if (parameters.Count == 0)
            {
                bool modify = false;
                foreach (string s in command) { parameters.Add(new string[] { s }); }
                foreach (string[] array in parameters)
                {
                    if (array[0] == "+" || array[0] == "-" || array[0] == "/" || array[0] == "*" || array[0] == "==" || array[0] == "!=")
                    {
                        modify = true;
                    }
                }
                if (modify)
                {
                    List<string> temp = new List<string>();
                    foreach (string[] parm in parameters) { temp.Add(parm[0]); }
                    parameters.Clear();
                    parameters.Add(temp.ToArray());
                }
            }

            dynamic result = ConcatenateValues(parameters[parameter], line, parseVar);
            return result;
        }

        public static bool VariableExists(string varName)
        {
            Variable var = Init.runtimeVariables.Find(v => v.name == varName);
            bool exists = var != null;
            return exists;
        }
        public static Variable FindVariableOfName(string varName)
        {
            return Init.runtimeVariables.Find(v => v.name == varName);
        }
    }
}
