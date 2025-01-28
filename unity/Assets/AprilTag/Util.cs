using System;
using Unity.Collections;
using UnityEngine;

public static class TextureExtensions
{
    public static ReadOnlySpan<Color32> GetPixelSpan(this Texture2D texture)
    {
        // Ensure the texture is readable
        if (!texture.isReadable)
        {
            throw new InvalidOperationException("Texture is not readable.");
        }

        // Get the raw texture data as a NativeArray<Color32>
        NativeArray<Color32> rawData = texture.GetRawTextureData<Color32>();

        // Convert the NativeArray to ReadOnlySpan and return
        return rawData.AsReadOnlySpan();
    }
}
