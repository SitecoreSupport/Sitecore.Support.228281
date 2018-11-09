using Sitecore;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Xml.Patch;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Sitecore.Support.Configuration
{

  /// <summary>
  /// ConfigReader that can read configuration basing on Role attributes.
  /// </summary>
  /// <seealso cref="T:Sitecore.Configuration.ConfigReader" />
  /// <summary>
  /// ConfigReader that can read configuration basing on Role attributes.
  /// </summary>
  /// <seealso cref="T:Sitecore.Configuration.ConfigReader" />
  [UsedImplicitly]
  public class RuleBasedConfigReader : Sitecore.Support.Configuration.ConfigReader, IConfigurationSectionHandler
  {
    /// <summary>
    /// Defines <see cref="T:Sitecore.Configuration.RuleBasedConfigReader.Builder" /> class that creates instances of <see cref="T:Sitecore.Configuration.RuleBasedConfigReader" />
    /// </summary>
    public class Builder
    {
      /// <summary>
      /// The built instance.
      /// </summary>
      private RuleBasedConfigReader builtInstance;

      /// <summary>
      /// Gets or sets the built instance.
      /// </summary>
      /// <value>
      /// The built instance.
      /// </value>
      public RuleBasedConfigReader BuiltInstance
      {
        get
        {
          if (builtInstance == null)
          {
            builtInstance = new RuleBasedConfigReader();
          }
          return builtInstance;
        }
        set
        {
          builtInstance = value;
        }
      }

      /// <summary>
      /// Sets the roles for the configuration reader that being is built by this <see cref="T:Sitecore.Configuration.RuleBasedConfigReader.Builder" /> instance.
      /// </summary>
      /// <param name="roles">The roles.</param>
      /// <returns>This <see cref="T:Sitecore.Configuration.RuleBasedConfigReader.Builder" /> instance to have chained calls for convenience.</returns>
      public Builder SetRoles(string[] roles)
      {
        Assert.ArgumentNotNull(roles, "roles");
        BuiltInstance.ConfigurationRoles = roles;
        return this;
      }

      /// <summary>
      /// Sets the include files for the configuration reader that is being built by this <see cref="T:Sitecore.Configuration.RuleBasedConfigReader.Builder" /> instance.
      /// </summary>
      /// <param name="includeFiles">The include files.</param>
      /// <returns>This <see cref="T:Sitecore.Configuration.RuleBasedConfigReader.Builder" /> instance to have chained calls for convenience.</returns>
      public Builder SetIncludeFiles(IEnumerable<string> includeFiles)
      {
        Assert.ArgumentNotNull(includeFiles, "includeFiles");
        BuiltInstance.IncludeFiles = includeFiles;
        return this;
      }
    }

    /// <summary>
    /// Allows to extend role mapping.
    /// </summary>
    protected interface IRoleMapper
    {
      /// <summary>
      /// Gets the additional roles for role.
      /// </summary>
      /// <param name="role">The role.</param>
      /// <returns></returns>
      string[] GetRolesForRole(string role);
    }

    /// <summary>
    /// Expands publishing role.
    /// </summary>
    /// <seealso cref="T:Sitecore.Configuration.RuleBasedConfigReader.IRoleMapper" />
    protected class PublishingRoleMapper : IRoleMapper
    {
      /// <summary>
      /// Gets the additional roles for publishing role.
      /// </summary>
      /// <param name="role">The role.</param>
      /// <returns></returns>
      public string[] GetRolesForRole(string role)
      {
        if (string.Compare(role, "dedicated-publishing", StringComparison.InvariantCultureIgnoreCase) == 0)
        {
          return new string[3]
          {
                    "publishing",
                    "publishing-1",
                    role
          };
        }
        return new string[0];
      }
    }

    /// <summary>
    /// Expands numberred role to numbered role + non-numbered one.
    /// </summary>
    /// <seealso cref="T:Sitecore.Configuration.RuleBasedConfigReader.IRoleMapper" />
    protected class NumericRoleMapper : IRoleMapper
    {
      private static Regex regex = new Regex("([^|;,]+)\\-(\\d+)$");

      /// <summary>
      /// Gets the additional roles for role.
      /// </summary>
      /// <param name="role">The role.</param>
      /// <returns></returns>
      public string[] GetRolesForRole(string role)
      {
        Match match = regex.Match(role);
        if (match.Success && match.Groups.Count >= 1)
        {
          List<string> list = new List<string>();
          string value = match.Groups[1].Value;
          list.Add(value);
          list.Add(role);
          return list.ToArray();
        }
        return new string[0];
      }
    }

    /// <summary>
    /// The role rule attribute name.
    /// </summary>
    public const string RoleRulePrefixName = "role";

    /// <summary>
    /// The search provider rule prefix name.
    /// </summary>
    public const string SearchProviderRulePrefixName = "search";

    /// <summary>
    /// The environment rule prefix name.
    /// </summary>
    public const string EnvironmentRulePrefixName = "env";

    /// <summary>
    /// The rule define suffix.
    /// </summary>
    public static readonly string RuleDefineSuffix = "define";

    /// <summary>
    /// The rule define suffix.
    /// </summary>
    public static readonly string RuleRequireSuffix = "require";

    /// <summary>
    /// The rule definitions.
    /// </summary>
    private readonly Dictionary<string, string[]> ruleDefinitions;

    /// <summary>
    /// Gets the configuration rules.
    /// </summary>
    /// <value>
    /// The configuration rules.
    /// </value>
    protected Dictionary<string, string[]> Rules => ruleDefinitions;

    /// <summary>
    /// Gets or sets the configuration roles.
    /// </summary>
    /// <value>
    /// The configuration roles.
    /// </value>
    protected virtual string[] ConfigurationRoles
    {
      get
      {
        return Rules["role"];
      }
      set
      {
        ValidateRoleCombination(value);
        Rules["role"] = value;
        NormalizeKnownRules(Rules);
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sitecore.Configuration.RuleBasedConfigReader" /> class.
    /// </summary>
    public RuleBasedConfigReader()
        : this(new LayeredConfigurationFiles())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sitecore.Configuration.RuleBasedConfigReader" /> class.
    /// </summary>
    /// <param name="includeFiles">The include files.</param>
    public RuleBasedConfigReader(IEnumerable<string> includeFiles)
        : this(includeFiles, ConfigurationManager.AppSettings)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sitecore.Configuration.RuleBasedConfigReader" /> class.
    /// </summary>
    /// <param name="includeFiles">The include files.</param>
    /// <param name="ruleCollection">The rule collection.</param>
    public RuleBasedConfigReader(IEnumerable<string> includeFiles, NameValueCollection ruleCollection)
        : base(includeFiles)
    {
      ruleDefinitions = ExtractReaderRules(ruleCollection);
      NormalizeKnownRules(ruleDefinitions);
    }

    /// <summary>
    /// Normalizes the known rules.
    /// </summary>
    /// <param name="rules">The rules.</param>
    protected void NormalizeKnownRules(Dictionary<string, string[]> rules)
    {
      if (rules != null)
      {
        NormalizeRoles(rules);
        NormalizeSearchProvider(rules);
        NormalizeEnvironment(rules);
      }
    }

    /// <summary>
    /// Normalizes the search provider.
    /// </summary>
    /// <param name="rules">The rules.</param>
    protected virtual void NormalizeSearchProvider(Dictionary<string, string[]> rules)
    {
      if (rules != null)
      {
        if (!rules.ContainsKey("search"))
        {
          rules.Add("search", new string[1]
          {
                    "lucene"
          });
        }
        else if (rules["search"] == null || rules["search"].Length == 0 || string.IsNullOrEmpty(rules["search"][0]))
        {
          rules["search"] = new string[1]
          {
                    "lucene"
          };
        }
      }
    }

    /// <summary>
    /// Normalizes the roles.
    /// </summary>
    /// <param name="rules">The rules.</param>
    protected virtual void NormalizeRoles(Dictionary<string, string[]> rules)
    {
      if (rules != null)
      {
        if (rules.ContainsKey("role"))
        {
          if (rules["role"] == null || rules["role"].Length == 0 || (rules["role"].Length == 1 && string.IsNullOrEmpty(rules["role"][0])))
          {
            rules["role"] = new string[1]
            {
                        SitecoreRole.Standalone.Name.ToLowerInvariant()
            };
          }
          else
          {
            rules["role"] = PreprocessConfiguredRoles(rules["role"]);
          }
        }
        else
        {
          rules.Add("role", new string[1]
          {
                    SitecoreRole.Standalone.Name.ToLowerInvariant()
          });
        }
      }
    }

    /// <summary>
    /// Normalizes the environment rules.
    /// </summary>
    /// <param name="rules">The rules.</param>
    protected virtual void NormalizeEnvironment(Dictionary<string, string[]> rules)
    {
      if (rules != null)
      {
        if (!rules.ContainsKey("env"))
        {
          rules.Add("env", new string[1]
          {
                    string.Empty
          });
        }
        else if (rules["env"] == null || rules["env"].Length == 0 || rules["env"][0] == null)
        {
          rules["env"] = new string[1]
          {
                    string.Empty
          };
        }
      }
    }

    /// <summary>
    /// Processes the collection with various settings and extracts rule definitions.
    /// </summary>
    /// <param name="ruleCollection">The rule collection.</param>
    /// <returns>Dictionary with rules.</returns>
    protected Dictionary<string, string[]> ExtractReaderRules(NameValueCollection ruleCollection)
    {
      Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>();
      if (ruleCollection == null)
      {
        return dictionary;
      }
      foreach (string key2 in ruleCollection.Keys)
      {
        string text2 = key2.Trim().ToLowerInvariant();
        if (text2.EndsWith($":{RuleDefineSuffix}", StringComparison.InvariantCultureIgnoreCase))
        {
          string[] configuredRules = GetConfiguredRules(ruleCollection[key2]);
          if (configuredRules != null)
          {
            string key = text2.Substring(0, text2.LastIndexOf(":", StringComparison.Ordinal));
            dictionary.Add(key, configuredRules);
          }
        }
      }
      return dictionary;
    }

    /// <summary>
    /// Validates the role combination.
    /// </summary>
    /// <param name="roles">The roles.</param>
    protected void ValidateRoleCombination(string[] roles)
    {
      if (roles.Contains("delivery"))
      {
        string text = roles.FirstOrDefault(delegate (string x)
        {
          if (x != "delivery")
          {
            return !x.StartsWith("delivery-", StringComparison.InvariantCultureIgnoreCase);
          }
          return false;
        });
        if (text != null)
        {
          throw new Sitecore.Exceptions.ConfigurationException($"The delivery role is specified alongside with {text} which is not supported.");
        }
      }
    }

    /// <summary>
    /// Gets the configured rules.
    /// </summary>
    /// <param name="ruleDefinition">The rule definition.</param>
    /// <returns><see cref="T:string[]" /> with names of configured rules, if any; otherwise, an empty <see cref="T:string[]" /></returns>
    protected virtual string[] GetConfiguredRules(string ruleDefinition)
    {
      if (ruleDefinition == null)
      {
        return Array.Empty<string>();
      }
      if (ruleDefinition.Length == 0)
      {
        return new string[1]
        {
                string.Empty
        };
      }
      return (from r in ruleDefinition.Split("|,;".ToCharArray())
              select Regex.Match(r, "^\\s*(\\S*)\\s*$").Groups[1].Value into s
              where s.Length > 0
              select s into x
              select x.ToLowerInvariant()).Distinct().ToArray();
    }

    /// <summary>
    /// Processes the configured roles. Depending on list of processors, some roles might gone or expanded to additional roles.
    /// </summary>
    /// <param name="roles">The roles.</param>
    /// <returns>New list of defined roles.</returns>
    protected virtual string[] PreprocessConfiguredRoles(string[] roles)
    {
      List<IRoleMapper> list = new List<IRoleMapper>(new IRoleMapper[2]
      {
            new NumericRoleMapper(),
            new PublishingRoleMapper()
      });
      List<string> list2 = new List<string>();
      foreach (string text in roles)
      {
        foreach (IRoleMapper item in list)
        {
          list2.AddRange(item.GetRolesForRole(text));
        }
        list2.Add(text);
      }
      return (from x in list2
              select x.ToLowerInvariant()).Distinct().ToArray();
    }

    /// <summary>
    /// Gets the configuration patcher.
    /// </summary>
    /// <param name="element">The xml node.</param>
    /// <returns>
    /// The configuration patcher.
    /// </returns>
    protected override ConfigPatcher GetConfigPatcher(XmlNode element)
    {
      return new ConfigPatcher(element, new XmlPatcher("http://www.sitecore.net/xmlconfig/set/", "http://www.sitecore.net/xmlconfig/", GetXmlPatchHelper()));
    }

    /// <summary>
    /// Gets the XML patch helper.
    /// </summary>
    /// <returns>The XML patch helper.</returns>
    protected virtual XmlPatchHelper GetXmlPatchHelper()
    {
      return new RuleBasedXmlPatchHelper(Rules);
    }

    /// <summary>
    /// Create config accessor.
    /// </summary>
    /// <param name="parent">The parent object.</param>
    /// <param name="configContext">Configuration context object.</param>
    /// <param name="section">The xml section.</param>
    /// <returns>The created section handler object.</returns>
    object IConfigurationSectionHandler.Create(object parent, object configContext, XmlNode section)
    {
      _section = section;
      Dictionary<string, string[]> rules = ExtractReaderRules(ConfigurationManager.AppSettings);
      NormalizeKnownRules(rules);
      typeof(Sitecore.Context).GetProperty("ConfigurationRules").SetValue(null, new ConfigurationRulesContext(rules));
      //Context.ConfigurationRules = new ConfigurationRulesContext(rules); Had to use reflection above to change the value of this property as it is static internal
      return this;
    }
  }
}