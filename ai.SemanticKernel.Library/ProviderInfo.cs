using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class ProviderInfo
{

   public const string DEFAULT = "default";

   private static List<ProviderConfig> _providers;
   public static List<ProviderConfig> Providers
   {
      get { return _providers; }
   }

   static ProviderInfo()
   {
      var config = new ConfigurationBuilder()
          .SetBasePath(AppContext.BaseDirectory)
          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
          .Build();
      var list = config.GetSection("Providers").Get<List<ProviderConfig>>();
      _providers = list ?? new List<ProviderConfig>();
   }

   /// <summary>
   /// Get Configuration by name.
   /// </summary>
   /// <param name="name">not case sensitive configuration name</param>
   /// <returns>instance of configuration if any was found with given name</returns>
   public static ProviderConfig? GetConfiguration(string name)
   {
      ProviderConfig? config = null;
      var lname = name.ToLower();
      foreach (var c in Providers)
      {
         if (lname == c.Name.ToLower())
         {
            config = c;
            break;
         }
      }
      return config;
   }

   /// <summary>
   /// Return default configuration.
   /// </summary>
   /// <returns>instance of configuration if any was default was found</returns>
   public static ProviderConfig? GetDefaultConfiguration()
   {
      return GetConfiguration(DEFAULT);
   }

}
