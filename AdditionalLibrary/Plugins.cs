using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;

namespace Additional
{
    public class Plugins
    {
        [ImportMany(typeof(IPlugin))]
        private IPlugin[] plugins;
        private string gatewayPath = "Gateways";

        public IPlugin[] Gateways
        {
            get { return plugins; }
            set { plugins = value; }
        }

        public void Load()
        {
            try
            {
                DirectoryCatalog catalog_plugin = new DirectoryCatalog(Path.Combine(Environment.CurrentDirectory, gatewayPath));
                CompositionContainer container = new CompositionContainer(catalog_plugin);
                CompositionBatch bath = new CompositionBatch();
                bath.AddPart(this);
                container.Compose(bath);
                string[] hows = new string[plugins.Length];
                int i = 0;
                foreach (var plugin in plugins)
                {
                    hows[i] = plugin.How;
                    i++;
                }

                Config.Hows = string.Join(",", hows);
            }
            catch(Exception ex)
            {
                throw new Exception($"Errors.ERROR_LOADING_GATEWAY, ex: {ex.Message}");
            }
        }
    }
}
