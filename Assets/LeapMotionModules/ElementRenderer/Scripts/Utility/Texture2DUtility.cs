﻿using System.Collections.Generic;
using UnityEngine;
using Leap.Unity;

public static class Texture2DUtility {

  public static List<TextureFormat> readWriteFormats = new List<TextureFormat>() {
    TextureFormat.ARGB32,
    TextureFormat.RGBA32,
    TextureFormat.RGB24,
    TextureFormat.Alpha8,
    TextureFormat.RGBAHalf,
    TextureFormat.RGFloat,
    TextureFormat.RHalf,
    TextureFormat.RGBAFloat,
    TextureFormat.RGFloat,
    TextureFormat.RFloat
  };

  public static Color GetPixelUnbounded(this Texture2D texture, int x, int y) {
    if (texture.wrapMode == TextureWrapMode.Clamp) {
      x = Mathf.Clamp(x, 0, texture.width - 1);
      y = Mathf.Clamp(y, 0, texture.height - 1);
    } else {
      x = Utils.Repeat(x, texture.width);
      y = Utils.Repeat(y, texture.height);
    }
    return texture.GetPixel(x, y);
  }

  public static void AddBorder(this Texture2D texture, int pixelAmount) {
    if (pixelAmount <= 0) return;

    texture.EnsureReadWriteEnabled();
    Color[] colors = texture.GetPixels();

    int originalWidth = texture.width;
    int originalHeight = texture.height;
    int newWidth = originalWidth + pixelAmount * 2;
    int newHeight = originalHeight + pixelAmount * 2;
    texture.Resize(newWidth, newHeight);

    texture.SetPixels(pixelAmount, pixelAmount, originalWidth, originalHeight, colors);

    for (int x = 0; x < newWidth; x++) {
      for (int dy = 0; dy < pixelAmount; dy++) {
        int innerX = x - pixelAmount;
        {
          int y = dy;
          int innerY = y - pixelAmount;
          texture.SetPixel(x, y, texture.GetPixelUnbounded(innerX, innerY));
        }
        {
          int y = texture.height - dy - 1;
          int innerY = y + pixelAmount;
          texture.SetPixel(x, y, texture.GetPixelUnbounded(innerX, innerY));
        }
      }
    }

    for (int y = pixelAmount; y < newWidth - pixelAmount; y++) {
      for (int dx = 0; dx < pixelAmount; dx++) {
        int innerY = y - pixelAmount;
        {
          int x = dx;
          int innerX = x - pixelAmount;
          texture.SetPixel(x, y, texture.GetPixelUnbounded(innerX, innerY));
        }
        {
          int x = texture.width - dx - 1;
          int innerX = x + pixelAmount;
          texture.SetPixel(x, y, texture.GetPixelUnbounded(innerX, innerY));
        }
      }
    }

    texture.Apply();
  }

}
