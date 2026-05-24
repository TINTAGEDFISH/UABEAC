using System;
using System.Linq;
using System.Reflection;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using UABEAvalonia;

namespace UABEAvalonia
{
    internal static class TexturePluginBridge
    {
        private static Type _th;
        private static Type _tie;

        private static Type TH => _th ??= FindType("TexturePlugin.TextureHelper");
        private static Type TIE => _tie ??= FindType("TexturePlugin.TextureImportExport");

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.FullName == fullName);
        }

        public static AssetTypeValueField GetByteArrayTexture(AssetWorkspace workspace, AssetContainer tex)
        {
            var method = TH?.GetMethod("GetByteArrayTexture", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AssetWorkspace), typeof(AssetContainer) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.GetByteArrayTexture 未找到，请确认 TexturePlugin 已加载。");
            return (AssetTypeValueField)method.Invoke(null, new object[] { workspace, tex });
        }

        public static bool GetResSTexture(object texFile, AssetsFileInstance fileInst)
        {
            var method = TH?.GetMethod("GetResSTexture", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(TextureFile), typeof(AssetsFileInstance) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.GetResSTexture 未找到。");
            return (bool)method.Invoke(null, new object[] { texFile, fileInst });
        }

        public static byte[] GetRawTextureBytes(object texFile, AssetsFileInstance inst)
        {
            var method = TH?.GetMethod("GetRawTextureBytes", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(TextureFile), typeof(AssetsFileInstance) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.GetRawTextureBytes 未找到。");
            return (byte[])method.Invoke(null, new object[] { texFile, inst });
        }

        public static byte[] GetPlatformBlob(AssetTypeValueField baseField)
        {
            var method = TH?.GetMethod("GetPlatformBlob", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AssetTypeValueField) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.GetPlatformBlob 未找到。");
            return (byte[])method.Invoke(null, new object[] { baseField });
        }

        public static bool IsPo2(int n)
        {
            var method = TH?.GetMethod("IsPo2", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.IsPo2 未找到。");
            return (bool)method.Invoke(null, new object[] { n });
        }

        public static int GetMaxMipCount(int width, int height)
        {
            var method = TH?.GetMethod("GetMaxMipCount", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int), typeof(int) }, null);
            if (method == null) throw new InvalidOperationException("TextureHelper.GetMaxMipCount 未找到。");
            return (int)method.Invoke(null, new object[] { width, height });
        }

        public static byte[] Import(string imagePath, TextureFormat format, out int width, out int height, ref int mips, uint platform, byte[] platformBlob)
        {
            var method = TIE?.GetMethod("Import", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(TextureFormat), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(uint), typeof(byte[]) }, null);
            if (method == null) throw new InvalidOperationException("TextureImportExport.Import(string...) 未找到，请确认 TexturePlugin 已加载。");
            var args = new object[] { imagePath, format, 0, 0, mips, platform, platformBlob };
            var result = method.Invoke(null, args);
            width = (int)args[2];
            height = (int)args[3];
            mips = (int)args[4];
            return (byte[])result;
        }

        public static bool Export(byte[] encData, string imagePath, int width, int height, TextureFormat format, uint platform, byte[] platformBlob)
        {
            var method = TIE?.GetMethod("Export", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(byte[]), typeof(string), typeof(int), typeof(int), typeof(TextureFormat), typeof(uint), typeof(byte[]) }, null);
            if (method == null) throw new InvalidOperationException("TextureImportExport.Export(byte[],string...) 未找到，请确认 TexturePlugin 已加载。");
            return (bool)method.Invoke(null, new object[] { encData, imagePath, width, height, format, platform, platformBlob });
        }
    }
}