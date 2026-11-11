using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Stagehand
{
    sealed class CaseSwitches
    {
        public bool LightsOff;
        public bool ScreensOff;
        public string LightingBefore = "";
        public bool CoolerScreenWasOn;
        public Dictionary<string, int> FanScreenBrightnessBefore = new Dictionary<string, int>();
        public bool CaseScreenWasDisplay;
        public int CaseScreenBrightnessBefore;
        public string ScreensSwitched = "";

        public const string FileName = "switches.json";
        static readonly string FilePath = Path.Combine(Log.DataDir, FileName);

        public static CaseSwitches Load()
        {
            return File.Exists(FilePath)
                ? new JavaScriptSerializer().Deserialize<CaseSwitches>(File.ReadAllText(FilePath, Encoding.UTF8))
                : new CaseSwitches();
        }

        void Save()
        {
            Directory.CreateDirectory(Log.DataDir);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
        }

        public const string CoolerScreen = "cooler", FanScreens = "fans", CaseScreen = "case";

        public static HashSet<string> Ids(string list) => new HashSet<string>((list ?? "").Split(',').Where(id => id.Length > 0));

        public async Task SetLightsOffAsync(bool off, string excluded)
        {
            if (off == LightsOff) return;
            if (off)
            {
                LightingBefore = LConnect.SavedLighting();
                Save();
                var black = LConnect.Json.Deserialize<Dictionary<string, object>>(LightingBefore);
                var skip = Ids(excluded);
                var keep = ((IEnumerable)black["MacList"]).Cast<object>().Select(id => !skip.Contains(Convert.ToString(id))).ToList();
                foreach (var list in new[] { "MacList", "DirectionList", "ColorTempList", "SelectedList" })
                    black[list] = ((IEnumerable)black[list]).Cast<object>().Where((item, index) => index >= keep.Count || keep[index]).ToList();
                var effect = (Dictionary<string, object>)black["LightEffectSet"];
                effect["UIEffect"] = 3;
                effect["UserColors"] = new[] { new Dictionary<string, object> { { "A", 255 }, { "R", 0 }, { "G", 0 }, { "B", 0 }, { "ScA", 1 }, { "ScR", 0 }, { "ScG", 0 }, { "ScB", 0 } } };
                await LConnect.Post("action=SetOpenRgbLightEffect", LConnect.Json.Serialize(black));
            }
            else await LConnect.Post("action=SetOpenRgbLightEffect", LightingBefore);
            LightsOff = off;
            Save();
        }

        public async Task SetScreensOffAsync(bool off, string excluded)
        {
            if (off == ScreensOff) return;
            var kinds = off ? new HashSet<string> { CoolerScreen, FanScreens, CaseScreen } : Ids(ScreensSwitched);
            if (off)
            {
                kinds.ExceptWith(Ids(excluded));
                ScreensSwitched = string.Join(",", kinds);
                CoolerScreenWasOn = false;
                if (kinds.Contains(CoolerScreen))
                    foreach (var cooler in await LConnect.Controllers(LConnect.CoolerType))
                        CoolerScreenWasOn |= (bool)await LConnect.Device(cooler, "GetIsScreenOn", "");
                FanScreenBrightnessBefore = new Dictionary<string, int>();
                if (kinds.Contains(FanScreens))
                    foreach (var fans in (await LConnect.Controllers(LConnect.FanScreenType)).Take(1))
                        foreach (var group in await LConnect.FanScreenGroups(fans))
                            FanScreenBrightnessBefore[(string)group["HubID"]] = Convert.ToInt32(group["Brightness"]);
                if (kinds.Contains(CaseScreen))
                {
                    var display = await LConnect.CaseScreen(LConnect.CaseScreenDisplayMode);
                    CaseScreenWasDisplay = display != null;
                    if (display != null) await LConnect.Device(display, "SetSecondaryScreen", "false");
                    var screen = display != null ? await LConnect.WaitForCaseScreen(LConnect.CaseScreenStandalone) : await LConnect.CaseScreen(LConnect.CaseScreenStandalone);
                    if (screen != null) CaseScreenBrightnessBefore = Convert.ToInt32(await LConnect.UntilBound(screen, "GetScreenBrightness", ""));
                }
                await ApplyScreensOffAsync();
            }
            else
            {
                if (kinds.Contains(CoolerScreen))
                    foreach (var cooler in await LConnect.Controllers(LConnect.CoolerType))
                        await LConnect.Device(cooler, "SetIsScreenOn", CoolerScreenWasOn ? "true" : "false");
                if (kinds.Contains(FanScreens))
                    foreach (var fans in (await LConnect.Controllers(LConnect.FanScreenType)).Take(1))
                        foreach (var group in FanScreenBrightnessBefore)
                            await LConnect.SetFanScreenBrightness(fans, group.Key, group.Value);
                var screen = kinds.Contains(CaseScreen) ? await LConnect.CaseScreen(LConnect.CaseScreenStandalone) : null;
                if (screen != null)
                {
                    await LConnect.UntilBound(screen, "SetScreenBrightness", CaseScreenBrightnessBefore.ToString());
                    if (CaseScreenWasDisplay) await LConnect.Device(screen, "SetSecondaryScreen", "true");
                }
            }
            ScreensOff = off;
            Save();
        }

        public async Task ApplyScreensOffAsync()
        {
            var kinds = Ids(ScreensSwitched);
            if (kinds.Contains(CoolerScreen))
                foreach (var cooler in await LConnect.Controllers(LConnect.CoolerType))
                    await LConnect.Device(cooler, "SetIsScreenOn", "false");
            if (kinds.Contains(FanScreens))
                foreach (var fans in (await LConnect.Controllers(LConnect.FanScreenType)).Take(1))
                    foreach (var group in await LConnect.FanScreenGroups(fans))
                        await LConnect.SetFanScreenBrightness(fans, (string)group["HubID"], 0);
            var screen = kinds.Contains(CaseScreen) ? await LConnect.CaseScreen(LConnect.CaseScreenStandalone) : null;
            if (screen != null) await LConnect.Device(screen, "SetScreenBrightness", "0");
        }

        public bool BlacksOutCaseScreen => ScreensOff && Ids(ScreensSwitched).Contains(CaseScreen);
    }

    static class LConnect
    {
        public const int CoolerType = 17104896, FanScreenType = 16998400;
        public const string CaseScreenDisplayMode = "vid_1a86&pid_ad26", CaseScreenStandalone = "vid_1cbe&pid_a092";
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11021/"), Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<string> Post(string query, string body)
        {
            var response = await Http.PostAsync("?" + query, new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<object> Device(string path, string type, string body)
        {
            var reply = Json.Deserialize<Dictionary<string, object>>(await Post("action=Device&devicePath=" + Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(path))) + "&type=" + type, body));
            if (!(bool)reply["Success"]) throw new InvalidOperationException(type + " failed: " + reply["Message"]);
            return reply["Data"];
        }

        public static string SavedLighting()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Lian-Li", "L-Connect 3", "device", Md5("openrgb"), Md5("openrgblighting") + ".0");
            using (var gzip = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
                return Json.Serialize(Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd())["Data"]);
        }

        static string Md5(string text)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }

        static async Task<Dictionary<string, object>> ControllerList()
        {
            return Json.Deserialize<Dictionary<string, object>>(await Post("action=SyncControllerList", ""));
        }

        public static async Task<List<string>> Controllers(int type)
        {
            return (await ControllerList()).Where(controller => Convert.ToInt32(controller.Value) == type).Select(controller => controller.Key).ToList();
        }

        public static async Task<string> CaseScreen(string usbId)
        {
            return (await ControllerList()).Keys.FirstOrDefault(path => path.IndexOf(usbId, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static async Task<string> WaitForCaseScreen(string usbId)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var path = await CaseScreen(usbId);
                if (path != null) return path;
                await Task.Delay(500);
            }
            throw new InvalidOperationException("the 9.2-inch screen didn't come back after switching modes");
        }

        public static async Task<object> UntilBound(string path, string type, string body)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await Device(path, type, body);
                }
                catch (InvalidOperationException) when (attempt < 30)
                {
                    await Task.Delay(500);
                }
            }
        }

        public static async Task<bool> Available()
        {
            try
            {
                return (await Post("action=Ping", "")).Contains("OK");
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException)
            {
                return false;
            }
        }

        public static async Task<List<KeyValuePair<string, string>>> LightingDevices()
        {
            var names = ((IEnumerable)Json.DeserializeObject(await Post("action=GetOpenRgbDevice", ""))).Cast<Dictionary<string, object>>()
                .ToDictionary(device => Convert.ToString(device["devIndex"]), device => (string)device["Name"] + " " + (Convert.ToInt32(device["devIndex"]) + 1));
            return ((IEnumerable)Json.Deserialize<Dictionary<string, object>>(SavedLighting())["MacList"]).Cast<object>().Select(Convert.ToString)
                .Select(id => new KeyValuePair<string, string>(id, names.TryGetValue(id, out var name) ? name : id == "HS2" ? "HydroShift II cooler" : "Wireless fans " + id.Substring(0, Math.Min(8, id.Length))))
                .ToList();
        }

        public static async Task<List<KeyValuePair<string, string>>> ScreenDevices()
        {
            var screens = new List<KeyValuePair<string, string>>();
            if ((await Controllers(CoolerType)).Count > 0) screens.Add(new KeyValuePair<string, string>(CaseSwitches.CoolerScreen, "Cooler screen"));
            if ((await Controllers(FanScreenType)).Count > 0) screens.Add(new KeyValuePair<string, string>(CaseSwitches.FanScreens, "LCD fan screens"));
            if (await CaseScreen(CaseScreenDisplayMode) != null || await CaseScreen(CaseScreenStandalone) != null) screens.Add(new KeyValuePair<string, string>(CaseSwitches.CaseScreen, "9.2-inch case screen"));
            return screens;
        }

        public static async Task<List<Dictionary<string, object>>> FanScreenGroups(string path)
        {
            return ((IEnumerable)await Device(path, "GetLCDScreensInfo", "")).Cast<Dictionary<string, object>>().ToList();
        }

        public static Task SetFanScreenBrightness(string path, string hubId, int brightness)
        {
            return Device(path, "SetBrightness", Json.Serialize(new Dictionary<string, object> { { "HubID", hubId }, { "Brightness", brightness } }));
        }
    }
}
