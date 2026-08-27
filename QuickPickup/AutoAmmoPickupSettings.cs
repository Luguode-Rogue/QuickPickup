using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using TaleWorlds.Library;

namespace QuickPickup
{
    public class AutoAmmoPickupSettings
    {
        private static string ConfigPath =>
            Path.Combine(BasePath.Name, "Modules", "QuickPickup", "QuickPickupSettings.xml");

        public float Radius = 5f;
        public bool OnlyAmmo = true;

        public static AutoAmmoPickupSettings Instance;

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Instance = GetDefault();
                    Save(); // ⭐ 自动生成配置文件
                    InformationManager.DisplayMessage(
                        new InformationMessage("[AutoAmmoPickup] Config generated."));
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(AutoAmmoPickupSettings));
                using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
                {
                    Instance = (AutoAmmoPickupSettings)serializer.Deserialize(fs);
                }
            }
            catch
            {
                Instance = GetDefault();
                Save(); // 出错也重建
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));

                XmlSerializer serializer = new XmlSerializer(typeof(AutoAmmoPickupSettings));
                using (FileStream fs = new FileStream(ConfigPath, FileMode.Create))
                {
                    serializer.Serialize(fs, Instance);
                }
            }
            catch
            {
                // 静默失败，避免影响游戏
            }
        }

        private static AutoAmmoPickupSettings GetDefault()
        {
            return new AutoAmmoPickupSettings
            {
                Radius = 5f,
                OnlyAmmo = true
            };
        }
    }

}
