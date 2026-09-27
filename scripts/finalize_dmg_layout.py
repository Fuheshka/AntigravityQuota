#!/usr/bin/env python3
import os
import sys
import subprocess
import time

def finalize_dmg(dmg_path="dist/AntigravityQuota-v1.0.0-macOS.dmg", vol_name="AntigravityQuota"):
    if len(sys.argv) > 1:
        dmg_path = sys.argv[1]
    if len(sys.argv) > 2:
        vol_name = sys.argv[2]

    print(f"=== Finalizing DMG Layout & Locking Finder Bounds for {dmg_path} ===")
    
    dist_dir = os.path.dirname(dmg_path) or "."
    rw_dmg = os.path.join(dist_dir, "temp_rw.dmg")
    
    if os.path.exists(rw_dmg):
        os.remove(rw_dmg)
        
    subprocess.run(["hdiutil", "convert", dmg_path, "-format", "UDRW", "-o", rw_dmg], check=True)
    
    # Mount temporary read-write DMG
    mount_output = subprocess.check_output(["hdiutil", "attach", rw_dmg, "-noautoopen", "-nobrowse"]).decode("utf-8")
    dev_name = ""
    mount_point = f"/Volumes/{vol_name}"
    
    for line in mount_output.splitlines():
        if "/Volumes/" in line:
            parts = line.split("\t")
            dev_name = parts[0].strip()
            mount_point = parts[-1].strip()
            
    print(f"Mounted RW volume at: {mount_point} ({dev_name})")
    
    # Apply chflags hidden & SetFile -a V to ALL hidden items
    hidden_files = [".background", ".VolumeIcon.icns", ".DS_Store", ".Trashes", ".fseventsd"]
    for hf in hidden_files:
        full_p = os.path.join(mount_point, hf)
        if os.path.exists(full_p):
            subprocess.run(["chflags", "hidden", full_p], check=False)
            subprocess.run(["SetFile", "-a", "V", full_p], check=False)
            
    # AppleScript to position hidden items slightly below window (y=500, NOT right) and lock window bounds to 660x440
    applescript_code = f'''
    tell application "Finder"
        tell disk "{vol_name}"
            open
            tell container window
                set current view to icon view
                set toolbar visible to false
                set statusbar visible to false
                set bounds to {{200, 120, 860, 560}}
            end tell
            
            -- Set explicit positions for app and Applications link
            try
                set position of item "{vol_name}.app" to {{160, 140}}
            end try
            try
                set position of item "Applications" to {{500, 140}}
            end try

            -- Position hidden items slightly below window bounds (y=500) so they NEVER cause horizontal rightward scrolling
            try
                set position of item ".background" to {{330, 500}}
            end try
            try
                set position of item ".VolumeIcon.icns" to {{330, 500}}
            end try
            
            set opts to icon view options of container window
            tell opts
                set icon size to 128
                set text size to 12
                set arrangement to not arranged
                set label position to bottom
            end tell
            update without registering applications
            close
        end tell
    end tell
    '''
    
    try:
        subprocess.run(["osascript", "-e", applescript_code], check=True)
        time.sleep(2)
    except Exception as e:
        print(f"Warning running Finder lock AppleScript: {e}")
        
    # Unmount RW DMG
    subprocess.run(["hdiutil", "detach", dev_name, "-force"], check=True)
    time.sleep(1)
    
    # Compress RW DMG back to final read-only DMG
    if os.path.exists(dmg_path):
        os.remove(dmg_path)
        
    subprocess.run(["hdiutil", "convert", rw_dmg, "-format", "UDZO", "-imagekey", "zlib-level=9", "-o", dmg_path], check=True)
    if os.path.exists(rw_dmg):
        os.remove(rw_dmg)
        
    print(f"🎉 DMG Finalization Complete! Positioned hidden items at y=500 and locked bounds to 660x440.")

if __name__ == "__main__":
    finalize_dmg()
