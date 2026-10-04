cask "antigravity-quota" do
  version "1.2.0"
  sha256 "96bf3cd3b2b684f5ecb979cc50b061bc79d48ac5acab74be076f5654d95a3281"

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
