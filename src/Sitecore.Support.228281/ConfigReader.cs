using Microsoft.Practices.EnterpriseLibrary.Common.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Xml;
using Sitecore.Collections;
using Sitecore.Diagnostics;
using Sitecore.Threading.Locks;
using Sitecore.Xml;
using System.Text.RegularExpressions;
using Sitecore;
using System.Linq;

namespace Sitecore.Support.Configuration
{
  public class ConfigReader : Sitecore.Configuration.ConfigReader
  {
    public ConfigReader(IEnumerable<string> includeFiles)
    {
      IncludeFiles = includeFiles;
    }


    /// <summary>
    /// Replaces global variables.
    /// </summary>
    /// <param name="rootNode">
    /// The root node.
    /// </param>
    protected override void ReplaceGlobalVariables([NotNull] XmlNode rootNode)
    {
      Assert.ArgumentNotNull(rootNode, "rootNode");

      XmlNodeList nodes = rootNode.SelectNodes(".//sc.variable");

      var variables = new StringDictionary();

      foreach (XmlAttribute attribute in rootNode.Attributes)
      {
        string name = attribute.Name;
        string value = StringUtil.GetString(attribute.Value);

        if (name.Length > 0)
        {
          string variable = "$(" + name + ")";

          variables[variable] = value;
        }
      }

      for (int n = 0; n < nodes.Count; n++)
      {
        string name = XmlUtil.GetAttribute("name", nodes[n]);
        string value = XmlUtil.GetAttribute("value", nodes[n]);

        if (name.Length > 0)
        {
          string variable = "$(" + name + ")";

          variables[variable] = value;
        }
      }

      if (variables.Count == 0)
      {
        return;
      }

      variables = ResolveVariables(variables);//THIS IS THE METHOD CALL INTRODUCED BY THE PATCH, TAKEN FROM SITECORE 9.1

      ReplaceGlobalVariables(rootNode, variables);
    }
    /// <summary>
    /// Resolves the global variables. Allows to reference from one var to other(s).
    /// </summary>
    /// <param name="variables">The variables.</param>
    /// <returns>Resolved global variables</returns>
    protected internal virtual StringDictionary ResolveVariables(StringDictionary variables)
    {
      var notResolvedVariables = new StringDictionary();
      var resolvedVariables = new StringDictionary();

      foreach (KeyValuePair<string, string> keyValuePair in variables)
      {
        if (keyValuePair.Value.IndexOf("$(", StringComparison.Ordinal) < 0)
        {
          resolvedVariables.Add(keyValuePair.Key, keyValuePair.Value);
        }
        else
        {
          notResolvedVariables.Add(keyValuePair.Key, keyValuePair.Value);
        }
      }

      while (notResolvedVariables.Count > 0)
      {
        var notResolvedVarsDetected = true;

        foreach (string notResolvedKey in notResolvedVariables.Keys.ToList())
        {
          string originalValue = notResolvedVariables[notResolvedKey];
          string value = originalValue;

          FindVariables(value).ForEach(v =>
          {
            if (resolvedVariables.ContainsKey(v))
            {
              value = value.Replace(v, resolvedVariables[v]);
            }
          });

          if (value != originalValue)
          {
            notResolvedVarsDetected = false;

            if (value.IndexOf("$(", StringComparison.Ordinal) < 0)
            {
              resolvedVariables.Add(notResolvedKey, value);
              notResolvedVariables.Remove(notResolvedKey);
            }
            else
            {
              notResolvedVariables[notResolvedKey] = value;
            }
          }
        }

        if (notResolvedVarsDetected)
        {
          foreach (var item in notResolvedVariables)
          {
            Log.Warn($"There is unresolved variable {item.Key} with {item.Value} value found in the config.", this);
          }

          break;
        }
      }

      resolvedVariables.AddRange(notResolvedVariables);

      return resolvedVariables;
    }

    private IEnumerable<string> FindVariables(string varValue)
    {
      var pat = @"\$\((.*?)\)";
      var m = Regex.Match(varValue, pat);

      while (m.Success)
      {
        yield return m.Groups[0].ToString();

        m = m.NextMatch();
      }
    }
  }
}