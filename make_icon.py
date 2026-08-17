from PIL import Image

def process_image(input_path, output_path):
    img = Image.open(input_path).convert("RGBA")
    data = img.getdata()
    
    new_data = []
    for item in data:
        r, g, b, a = item
        if r > 230 and g > 230 and b > 230:
            if r > 245 and g > 245 and b > 245:
                new_data.append((255, 255, 255, 0))
            else:
                alpha = min(255, max(0, (255 - r) * 5))
                new_data.append((r, g, b, alpha))
        else:
            new_data.append(item)
            
    img.putdata(new_data)
    
    bbox = img.getbbox()
    if bbox:
        img = img.crop(bbox)
        
    width, height = img.size
    size = max(width, height)
    new_img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    offset = ((size - width) // 2, (size - height) // 2)
    new_img.paste(img, offset)
    
    new_img.save(output_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])

process_image(r"C:\Users\Shawn\.gemini\antigravity-ide\brain\60ba87f9-9d98-4438-869c-9026cae57f32\fox_wifi_transparent_1786955571610.png", r"icon.ico")
