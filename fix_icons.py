import os
from PIL import Image

folder = r"C:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\Resources\Icons"

for filename in os.listdir(folder):
    if filename.endswith(".png"):
        path = os.path.join(folder, filename)
        img = Image.open(path).convert("RGBA")
        
        # Get background color from top-left pixel
        datas = img.getdata()
        bg_color = datas[0] # assuming top-left is background
        bg_rgb = bg_color[:3]
        
        newData = []
        tolerance = 25 # tolerance for background color match
        for item in datas:
            if abs(item[0]-bg_rgb[0]) < tolerance and abs(item[1]-bg_rgb[1]) < tolerance and abs(item[2]-bg_rgb[2]) < tolerance:
                # Transparent
                newData.append((255, 255, 255, 0))
            else:
                newData.append(item)
                
        img.putdata(newData)
        
        # Resize to 64x64 with Lanczos for high quality
        # This fixes Revit rendering 1024x1024 down to 32x32 poorly
        img = img.resize((64, 64), Image.Resampling.LANCZOS)
        
        img.save(path, "PNG")
        print(f"Processed {filename}")
