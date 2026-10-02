cask "antigravity-quota" do
  version "1.2.0"
  sha256 "92bfacf84d587573184ad361c83ac0e7f70893ef6224ef709da4e66736c0aa67"

  url "https://github.com/Fuheshka/AntigravityQuota/releases/download/v#{version}/AntigravityQuota-v#{version}-macOS.dmg"
  name "Antigravity Quota"
  desc "Native macOS MenuBar & HUD quota monitor for Google Antigravity"
  homepage "https://github.com/Fuheshka/AntigravityQuota"

  app "AntigravityQuota.app"
  binary "#{appdir}/AntigravityQuota.app/Contents/MacOS/AntigravityQuota", target: "antigravity-quota"

  zap trash: [
    "~/Library/Application Support/AntigravityQuota",
    "~/Library/Preferences/com.fuheshka.antigravity-quota.plist",
  ]
end
