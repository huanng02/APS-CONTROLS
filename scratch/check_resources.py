import os
import re

def find_xaml_files(root_dir):
    xaml_files = []
    for root, dirs, files in os.walk(root_dir):
        # Skip obj, bin, .git, etc.
        if any(x in root.split(os.sep) for x in ['obj', 'bin', '.git', '.vs', 'scratch']):
            continue
        for file in files:
            if file.endswith('.xaml'):
                xaml_files.append(os.path.join(root, file))
    return xaml_files

def analyze_resources(xaml_files):
    defined_keys = set()
    references = []

    # Regex patterns
    # x:Key="key_name" or Key="key_name"
    key_pattern = re.compile(r'(?:x:)?Key=["\']([^"\']+)["\']')
    # {StaticResource key_name} or {StaticResource ResourceKey=key_name}
    static_ref_pattern = re.compile(r'\{StaticResource\s+(?:ResourceKey=)?([a-zA-Z0-9_]+)\}')

    # Standard system/WPF keys to ignore
    ignored_keys = {
        'HorizontalScrollBarVisibility', 'VerticalScrollBarVisibility',
        'Top', 'Left', 'Right', 'Bottom', 'Hand', 'Arrow', 'Wait',
        'BackgroundProperty', 'ForegroundProperty', 'BorderBrushProperty',
        'DisabledOpacity', 'ButtonBackground', 'ButtonBorder',
        'SystemParameters', 'SystemColors'
    }

    # First pass: find all defined keys
    for filepath in xaml_files:
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
                # Find all keys
                keys = key_pattern.findall(content)
                for k in keys:
                    defined_keys.add(k)
        except Exception as e:
            print(f"Error reading {filepath}: {e}")

    # Add standard/builtin WPF resources if needed (or we will check them dynamically)
    # Let's print some stats
    print(f"Found {len(defined_keys)} defined resource keys in workspace.")

    # Second pass: find all StaticResource references and their lines
    for filepath in xaml_files:
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                lines = f.readlines()
                for line_idx, line in enumerate(lines):
                    matches = static_ref_pattern.findall(line)
                    for key in matches:
                        if key not in defined_keys and key not in ignored_keys:
                            # Verify if it starts with SystemColors or SystemParameters
                            if key.startswith('System') or key.endswith('Color') or key.endswith('Brush') or key.endswith('Style'):
                                references.append({
                                    'file': filepath,
                                    'line': line_idx + 1,
                                    'key': key,
                                    'content': line.strip()
                                })
        except Exception as e:
            print(f"Error reading {filepath} in second pass: {e}")

    return defined_keys, references

if __name__ == "__main__":
    workspace = r"d:\aps\aps\APS-CONTROLS"
    files = find_xaml_files(workspace)
    print(f"Found {len(files)} XAML files.")
    
    defined, missing = analyze_resources(files)
    
    if missing:
        print("\n=== POTENTIAL MISSING STATIC RESOURCES ===")
        for item in missing:
            print(f"File: {item['file']}")
            print(f"Line {item['line']}: {item['content']}")
            print(f"Key: {item['key']}\n")
    else:
        print("\nNo missing StaticResource references found!")
