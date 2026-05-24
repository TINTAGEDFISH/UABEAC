using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HarfBuzzSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class AssetTypeIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AssetClassID assetClass)
            {
                if ((int)assetClass < 0)
                {
                    return GetBitmap("Assets/Icons/asset-mono-behaviour.png");
                }

                return assetClass switch
                {
                    AssetClassID.Animation => GetBitmap("Assets/Icons/asset-animation.png"),
                    AssetClassID.AnimationClip => GetBitmap("Assets/Icons/asset-animation-clip.png"),
                    AssetClassID.Animator => GetBitmap("Assets/Icons/asset-animator.png"),
                    AssetClassID.AnimatorController => GetBitmap("Assets/Icons/asset-animator-controller.png"),
                    AssetClassID.AnimatorOverrideController => GetBitmap("Assets/Icons/asset-animator-override-controller.png"),
                    AssetClassID.AudioClip => GetBitmap("Assets/Icons/asset-audio-clip.png"),
                    AssetClassID.AudioListener => GetBitmap("Assets/Icons/asset-audio-listener.png"),
                    AssetClassID.AudioMixer => GetBitmap("Assets/Icons/asset-audio-mixer.png"),
                    AssetClassID.AudioMixerGroup => GetBitmap("Assets/Icons/asset-audio-mixer-group.png"),
                    AssetClassID.AudioSource => GetBitmap("Assets/Icons/asset-audio-source.png"),
                    AssetClassID.Avatar => GetBitmap("Assets/Icons/asset-avatar.png"),
                    AssetClassID.BillboardAsset => GetBitmap("Assets/Icons/asset-billboard.png"),
                    AssetClassID.BillboardRenderer => GetBitmap("Assets/Icons/asset-billboard-renderer.png"),
                    AssetClassID.BoxCollider => GetBitmap("Assets/Icons/asset-box-collider.png"),
                    AssetClassID.Camera => GetBitmap("Assets/Icons/asset-camera.png"),
                    AssetClassID.Canvas => GetBitmap("Assets/Icons/asset-canvas.png"),
                    AssetClassID.CanvasGroup => GetBitmap("Assets/Icons/asset-canvas-group.png"),
                    AssetClassID.CanvasRenderer => GetBitmap("Assets/Icons/asset-canvas-renderer.png"),
                    AssetClassID.CapsuleCollider => GetBitmap("Assets/Icons/asset-capsule-collider.png"),
                    AssetClassID.CapsuleCollider2D => GetBitmap("Assets/Icons/asset-capsule-collider.png"),
                    AssetClassID.ComputeShader => GetBitmap("Assets/Icons/asset-compute-shader.png"),
                    AssetClassID.Cubemap => GetBitmap("Assets/Icons/asset-cubemap.png"),
                    AssetClassID.Flare => GetBitmap("Assets/Icons/asset-flare.png"),
                    AssetClassID.FlareLayer => GetBitmap("Assets/Icons/asset-flare-layer.png"),
                    AssetClassID.Font => GetBitmap("Assets/Icons/asset-font.png"),
                    AssetClassID.GameObject => GetBitmap("Assets/Icons/asset-game-object.png"),
                    AssetClassID.Light => GetBitmap("Assets/Icons/asset-light.png"),
                    AssetClassID.LightmapSettings => GetBitmap("Assets/Icons/asset-lightmap-settings.png"),
                    AssetClassID.LODGroup => GetBitmap("Assets/Icons/asset-lod-group.png"),
                    AssetClassID.Material => GetBitmap("Assets/Icons/asset-material.png"),
                    AssetClassID.Mesh => GetBitmap("Assets/Icons/asset-mesh.png"),
                    AssetClassID.MeshCollider => GetBitmap("Assets/Icons/asset-mesh-collider.png"),
                    AssetClassID.MeshFilter => GetBitmap("Assets/Icons/asset-mesh-filter.png"),
                    AssetClassID.MeshRenderer => GetBitmap("Assets/Icons/asset-mesh-renderer.png"),
                    AssetClassID.MonoBehaviour => GetBitmap("Assets/Icons/asset-mono-behaviour.png"),
                    AssetClassID.MonoScript => GetBitmap("Assets/Icons/asset-mono-script.png"),
                    AssetClassID.NavMeshSettings => GetBitmap("Assets/Icons/asset-nav-mesh-settings.png"),
                    AssetClassID.ParticleSystem => GetBitmap("Assets/Icons/asset-particle-system.png"),
                    AssetClassID.ParticleSystemRenderer => GetBitmap("Assets/Icons/asset-particle-system-renderer.png"),
                    AssetClassID.RectTransform => GetBitmap("Assets/Icons/asset-rect-transform.png"),
                    AssetClassID.ReflectionProbe => GetBitmap("Assets/Icons/asset-reflection-probe.png"),
                    AssetClassID.Rigidbody => GetBitmap("Assets/Icons/asset-rigidbody.png"),
                    AssetClassID.Shader => GetBitmap("Assets/Icons/asset-shader.png"),
                    AssetClassID.ShaderVariantCollection => GetBitmap("Assets/Icons/asset-shader-collection.png"),
                    AssetClassID.SkinnedMeshRenderer => GetBitmap("Assets/Icons/asset-mesh-renderer.png"),
                    AssetClassID.Sprite => GetBitmap("Assets/Icons/asset-sprite.png"),
                    AssetClassID.SpriteRenderer => GetBitmap("Assets/Icons/asset-sprite-renderer.png"),
                    AssetClassID.Terrain => GetBitmap("Assets/Icons/asset-terrain.png"),
                    AssetClassID.TerrainCollider => GetBitmap("Assets/Icons/asset-terrain-collider.png"),
                    AssetClassID.TextAsset => GetBitmap("Assets/Icons/asset-text-asset.png"),
                    AssetClassID.Texture2D => GetBitmap("Assets/Icons/asset-texture2d.png"),
                    AssetClassID.Texture3D => GetBitmap("Assets/Icons/asset-texture2d.png"),
                    AssetClassID.Transform => GetBitmap("Assets/Icons/asset-transform.png"),
                    _ => GetBitmap("Assets/Icons/asset-unknown.png"),
                };
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }


        Dictionary<string, Bitmap> cache = new();

        private Bitmap GetBitmap(string relativePath)
        {
            if (cache.TryGetValue(relativePath, out var bitmap))
            {
                return bitmap;
            }

            // ensure no leading slash
            relativePath = relativePath.TrimStart('/');

            // build avares uri using this assembly's name so it survives AssemblyName changes
            string asmName = typeof(AssetTypeIconConverter).Assembly.GetName().Name ?? "UABEAvalonia";
            var uri = new Uri($"avares://{asmName}/{relativePath}");
            bitmap = new Bitmap(AssetLoader.Open(uri));
            cache[relativePath] = bitmap;
            return bitmap;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}