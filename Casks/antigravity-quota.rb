cask "antigravity-quota" do
  version "1.1.0"
  sha256 "ba1452b2561c93e05d93e82dc954a0212595fa292f23018dc180411079a0a5b0"

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
