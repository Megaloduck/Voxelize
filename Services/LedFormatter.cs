using SkiaSharp;
using System.Text;

namespace Voxelize.Services
{
    public static class LedFormatter
    {
        public static string Format(List<SKColor> colors, int gridW, int gridH, string format)
        {
            return format switch
            {
                "Arduino / FastLED" => FastLED(colors, gridW),
                "Arduino / NeoPixel" => NeoPixel(colors, gridW),
                "Raw Hex Array" => RawHex(colors, gridW),
                "Python / MicroPython" => MicroPython(colors, gridW),
                "Python / Pillow RGB" => PillowRgb(colors, gridW),
                "CSS Hex" => CssHex(colors, gridW),
                "JSON" => Json(colors, gridW),
                "C File (.c)" => CFile(colors, gridW, gridH),
                "WLED JSON" => WledJson(colors, gridW),
                "cURL" => Curl(colors, gridW),
                "Home Assistant YAML" => HomeAssistantYaml(colors, gridW),
                _ => RawHex(colors, gridW)
            };
        }

        // ── Helpers ──────────────────────────────────────────────────
        private static string Hex(SKColor c) => $"0x{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
        private static string CssHexStr(SKColor c) => $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
        private static string QuotedHex(SKColor c) => $"\"{CssHexStr(c)}\"";

        // Splits flat list into rows of gridW
        private static IEnumerable<IEnumerable<SKColor>> Rows(List<SKColor> colors, int gridW)
        {
            for (int i = 0; i < colors.Count; i += gridW)
                yield return colors.Skip(i).Take(gridW);
        }

        // ── Formats ──────────────────────────────────────────────────

        // Arduino / FastLED
        // CRGB leds[] = {
        //   0xFF0000, 0x00FF00,  // row 0
        //   ...
        // };
        private static string FastLED(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"#define NUM_LEDS {colors.Count}");
            sb.AppendLine("CRGB leds[] = {");
            int row = 0;
            foreach (var r in Rows(colors, gridW))
            {
                sb.Append("  ");
                sb.Append(string.Join(", ", r.Select(Hex)));
                sb.AppendLine($",  // row {row++}");
            }
            sb.AppendLine("};");
            return sb.ToString();
        }

        // Arduino / NeoPixel
        // strip.setPixelColor(index, r, g, b);
        private static string NeoPixel(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Total pixels: {colors.Count}");
            sb.AppendLine("void setFrame() {");
            for (int i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                sb.AppendLine($"  strip.setPixelColor({i}, {c.Red}, {c.Green}, {c.Blue});  // [{i / gridW},{i % gridW}]");
            }
            sb.AppendLine("  strip.show();");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Raw Hex Array — each row on its own line
        private static string RawHex(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            foreach (var row in Rows(colors, gridW))
                sb.AppendLine(string.Join(", ", row.Select(Hex)));
            return sb.ToString().TrimEnd();
        }

        // Python / MicroPython
        // pixels = [
        //   0xFF0000, 0x00FF00,  # row 0
        // ]
        private static string MicroPython(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {colors.Count} pixels  ({gridW} per row)");
            sb.AppendLine("pixels = [");
            int row = 0;
            foreach (var r in Rows(colors, gridW))
            {
                sb.Append("  ");
                sb.Append(string.Join(", ", r.Select(Hex)));
                sb.AppendLine($",  # row {row++}");
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        // Python / Pillow RGB tuples
        // pixels = [
        //   (255,0,0), (0,255,0),  # row 0
        // ]
        private static string PillowRgb(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine("pixels = [");
            int row = 0;
            foreach (var r in Rows(colors, gridW))
            {
                sb.Append("  ");
                sb.Append(string.Join(", ", r.Select(c => $"({c.Red},{c.Green},{c.Blue})")));
                sb.AppendLine($",  # row {row++}");
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        // CSS Hex — each row on its own line
        private static string CssHex(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            foreach (var row in Rows(colors, gridW))
                sb.AppendLine(string.Join(", ", row.Select(CssHexStr)));
            return sb.ToString().TrimEnd();
        }

        // JSON flat array of hex strings
        private static string Json(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"width\": {gridW},");
            sb.AppendLine($"  \"height\": {colors.Count / gridW},");
            sb.AppendLine("  \"pixels\": [");
            int row = 0;
            var rowList = Rows(colors, gridW).ToList();
            foreach (var r in rowList)
            {
                sb.Append("    ");
                sb.Append(string.Join(", ", r.Select(QuotedHex)));
                sb.Append(row < rowList.Count - 1 ? "," : "");
                sb.AppendLine($"  // row {row++}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // C File (.c)
        private static string CFile(List<SKColor> colors, int gridW, int gridH)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine();
            sb.AppendLine($"#define GRID_W {gridW}");
            sb.AppendLine($"#define GRID_H {gridH}");
            sb.AppendLine($"#define NUM_PIXELS {colors.Count}");
            sb.AppendLine();
            sb.AppendLine("const uint32_t frame[NUM_PIXELS] = {");
            int row = 0;
            foreach (var r in Rows(colors, gridW))
            {
                sb.Append("  ");
                sb.Append(string.Join(", ", r.Select(Hex)));
                sb.AppendLine($",  /* row {row++} */");
            }
            sb.AppendLine("};");
            return sb.ToString();
        }

        // WLED JSON — uses the "seg" individual LED override format
        // POST to http://<wled-ip>/json/state
        private static string WledJson(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"on\": true,");
            sb.AppendLine("  \"bri\": 255,");
            sb.AppendLine("  \"seg\": [{");
            sb.AppendLine("    \"i\": [");

            for (int i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                bool last = i == colors.Count - 1;
                sb.AppendLine($"      {i}, \"{c.Red:X2}{c.Green:X2}{c.Blue:X2}\"{(last ? "" : ",")}  // [{i / gridW},{i % gridW}]");
            }

            sb.AppendLine("    ]");
            sb.AppendLine("  }]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // cURL — ready to paste, targets WLED JSON API
        private static string Curl(List<SKColor> colors, int gridW)
        {
            var iArray = new StringBuilder();
            for (int i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                if (i > 0) iArray.Append(",");
                iArray.Append($"{i},\"{c.Red:X2}{c.Green:X2}{c.Blue:X2}\"");
            }

            var sb = new StringBuilder();
            sb.AppendLine("curl -X POST http://<WLED_IP>/json/state \\");
            sb.AppendLine("  -H \"Content-Type: application/json\" \\");
            sb.AppendLine("  -d '{");
            sb.AppendLine("    \"on\": true,");
            sb.AppendLine("    \"bri\": 255,");
            sb.AppendLine("    \"seg\": [{");
            sb.AppendLine($"      \"i\": [{iArray}]");
            sb.AppendLine("    }]");
            sb.AppendLine("  }'");
            return sb.ToString();
        }

        // Home Assistant YAML — uses light.turn_on with an effect or RGB sequence
        private static string HomeAssistantYaml(List<SKColor> colors, int gridW)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Paste into a Home Assistant script or automation action");
            sb.AppendLine("# Requires WLED integration or REST command");
            sb.AppendLine("service: rest_command.wled_set_frame");
            sb.AppendLine("data:");
            sb.AppendLine("  payload: |");
            sb.AppendLine("    {");
            sb.AppendLine("      \"on\": true,");
            sb.AppendLine("      \"bri\": 255,");
            sb.AppendLine("      \"seg\": [{");
            sb.AppendLine("        \"i\": [");

            for (int i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                bool last = i == colors.Count - 1;
                sb.AppendLine($"          {i}, \"{c.Red:X2}{c.Green:X2}{c.Blue:X2}\"{(last ? "" : ",")}");
            }

            sb.AppendLine("        ]");
            sb.AppendLine("      }]");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("# Add this to configuration.yaml to enable the REST command:");
            sb.AppendLine("# rest_command:");
            sb.AppendLine("#   wled_set_frame:");
            sb.AppendLine("#     url: http://<WLED_IP>/json/state");
            sb.AppendLine("#     method: POST");
            sb.AppendLine("#     content_type: application/json");
            sb.AppendLine("#     payload: '{{ payload }}'");
            return sb.ToString();
        }
    }
}