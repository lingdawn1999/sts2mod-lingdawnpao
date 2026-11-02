using System;
using System.Reflection;
using System.Linq;
using Godot;

public partial class Runner : Node
{
    public override void _Ready()
    {
        try
        {
            // The real burial path logs through Godot.OS; execute it with the
            // engine initialized rather than changing production logging for a test.
            System.Environment.SetEnvironmentVariable("STS2_NATIVE_BURIAL_TEST", "1");
            // Choice UI is absent in this headless fixture; keep the actual selection/effects.
            new HarmonyLib.Harmony("lingdawnpao.tests.choice-prompt").Patch(
                HarmonyLib.AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Models.CardModel), "SelectionScreenPrompt"),
                prefix: new HarmonyLib.HarmonyMethod(typeof(Runner), nameof(ChoicePromptStub)));
            if (OS.GetCmdlineUserArgs().Contains("--verify-relic-assets"))
            {
                const string root = "D:/sts2mod/lingdawnpao/";
                if (!ProjectSettings.LoadResourcePack(root + "build/lingdawnpao.pck"))
                    throw new Exception("Cannot mount exported relic assets");
                foreach (string path in new[] {
                    "res://images/atlases/relic_atlas.sprites/lingdawnpao_survival.tres",
                    "res://images/atlases/relic_outline_atlas.sprites/lingdawnpao_survival.tres",
                    "res://images/relics/lingdawnpao_survival.svg" })
                {
                    var texture = GD.Load<Texture2D>(path);
                    if (texture == null || texture.GetWidth() != 256 || texture.GetHeight() != 256)
                        throw new Exception("Invalid relic texture: " + path);
                    if (path == "res://images/atlases/relic_atlas.sprites/lingdawnpao_survival.tres")
                        texture.GetImage().SavePng(root + "build/snowfield-instinct-preview.png");
                }
                GD.Print("PASS: 3 exported relic textures.");
                var initiativePortrait = GD.Load<Texture2D>(
                    "res://images/atlases/card_atlas.sprites/lingdawnpao/lingdawnpao_initiative.tres");
                if (initiativePortrait == null || initiativePortrait.GetWidth() != 1438 || initiativePortrait.GetHeight() != 1093)
                    throw new Exception("Invalid Seize the Initiative portrait in exported PCK");
                var readyPortrait = GD.Load<Texture2D>(
                    "res://images/packed/card_portraits/lingdawnpao/ready_for_anything.png");
                if (readyPortrait == null || readyPortrait.GetWidth() != 1000 || readyPortrait.GetHeight() != 760)
                    throw new Exception("Invalid Ready for Anything portrait in exported PCK");
                var tossPortrait = GD.Load<Texture2D>(
                    "res://images/packed/card_portraits/lingdawnpao/frost_strike.png");
                if (tossPortrait == null || tossPortrait.GetWidth() != 1000 || tossPortrait.GetHeight() != 760)
                    throw new Exception("Snowball Toss's replacement portrait is missing or has unexpected dimensions");
                foreach (string language in new[] { "zhs", "eng" })
                {
                    string cardJson = Godot.FileAccess.GetFileAsString($"res://lingdawnpao/localization/{language}/cards.json");
                    string powerJson = Godot.FileAccess.GetFileAsString($"res://lingdawnpao/localization/{language}/powers.json");
                    using var cards = System.Text.Json.JsonDocument.Parse(cardJson);
                    using var powers = System.Text.Json.JsonDocument.Parse(powerJson);
                    string initiativeName = language == "zhs" ? "抢占先机" : "Seize the Initiative";
                    string echoName = language == "zhs" ? "霜冻回响" : "Frost Echo";
                    if (cards.RootElement.GetProperty("LINGDAWNPAO_INITIATIVE.title").GetString() != initiativeName
                        || cards.RootElement.GetProperty("FROZEN_ADEPT_CARD.title").GetString() != echoName
                        || powers.RootElement.GetProperty("FROZEN_ADEPT_POWER.title").GetString() != echoName)
                        throw new Exception("New card or renamed power localization missing from exported PCK");
                    string readyDescription = cards.RootElement.GetProperty("READY_FOR_ANYTHING.description").GetString()!;
                    if (!readyDescription.Contains("{Block:diff()}") || readyDescription.Contains("{Damage"))
                        throw new Exception("Ready for Anything must describe Block rather than damage in both languages");
                    foreach (var textCheck in new[] {
                        ("HUNT_OPERATION.description", "{CalculationExtra:diff()}"),
                        ("SNEAK_ATTACK.description", "{AmbushWeak:diff()}"),
                        ("DAM_BREAK.description", "{AmbushWeak:diff()}"),
                        ("THROAT_GRIP.description", "{AmbushVulnerable:diff()}"),
                        ("AMBUSH_BARRAGE.effect.SneakAttack", "{Secondary}"),
                        ("AMBUSH_BARRAGE.effect.DamBreak", "{Secondary}"),
                        ("AMBUSH_BARRAGE.effect.ThroatGrip", "{Secondary}") })
                        if (!cards.RootElement.GetProperty(textCheck.Item1).GetString()!.Contains(textCheck.Item2))
                            throw new Exception("Outdated card or collected-effect localization: " + textCheck.Item1);
                    if (cards.RootElement.GetProperty("DAM_BREAK.title").GetString() != (language == "zhs" ? "奇袭" : "Surprise Attack"))
                        throw new Exception("Surprise Attack has not replaced the old Dam Burst title");
                    if (!powers.RootElement.GetProperty("TRACKED_POWER.description").GetString()!.Contains("50%")
                        || !powers.RootElement.GetProperty("TRACKED_POWER.smartDescription").GetString()!.Contains("50%"))
                        throw new Exception("Hunted's exported status text must match its 50% native damage multiplier");
                    var storagePhrase = language == "zhs" ? "此次不触发" : "without triggering";
                    if (!cards.RootElement.GetProperty("FROST_DOUBLING.description").GetString()!.Contains(storagePhrase)
                        || !cards.RootElement.GetProperty("DEEP_WINTER.description").GetString()!.Contains(storagePhrase))
                        throw new Exception("Frost storage descriptions must explicitly state that they do not trigger frost");
                    var supply = cards.RootElement.GetProperty("FROST_STRIKE.description").GetString()!;
                    if (cards.RootElement.GetProperty("FROST_STRIKE.title").GetString() != (language == "zhs" ? "扔雪球" : "Snowball Toss"))
                        throw new Exception("Snowball Toss's display name is outdated in exported PCK");
                    var net = cards.RootElement.GetProperty("CLOSE_THE_NET.description").GetString()!;
                    if (!supply.Contains("{Cards:diff()}") || supply.Contains("{FrostbitePower")
                        || !supply.Contains("{IfUpgraded:show:") || !net.Contains("{Block:diff()}")
                        || net.Contains("{StrengthPower")
                        || !cards.RootElement.TryGetProperty("CLOSE_THE_NET.selectionScreenPrompt", out _))
                        throw new Exception("New supply or defensive net localization missing from exported PCK");
                    string newTrigger = language == "zhs" ? "每回合首次" : "first card";
                    if (!cards.RootElement.GetProperty("NIGHT_PATROL.description").GetString()!.Contains(newTrigger)
                        || !powers.RootElement.GetProperty("NIGHT_PATROL_POWER.smartDescription").GetString()!.Contains(newTrigger))
                        throw new Exception("Dark Harvest's exported card/power trigger is outdated");
                    string turn = language == "zhs" ? "本回合" : "this turn";
                    foreach (string key in new[] { "CHAIN_CLAW.description", "AMBUSH_BARRAGE.effect.ChainClaw" })
                        if (!cards.RootElement.GetProperty(key).GetString()!.Contains(turn)
                            || !cards.RootElement.GetProperty(key).GetString()!.Contains("0"))
                            throw new Exception("Chain Claw and its inherited effect must describe turn-limited free attacks");
                    string counter = powers.RootElement.GetProperty("CHAIN_CLAW_DISCOUNT_POWER.smartDescription").GetString()!;
                    if (!counter.Contains("{Amount}") || !counter.Contains(turn) || !counter.Contains("0"))
                        throw new Exception("Chain Claw's power must display the number of free attacks remaining this turn");
                }
                GD.Print("PASS: exported Initiative portrait and bilingual card/power names.");
                GD.Print("PASS: exported Ready for Anything portrait and bilingual defensive descriptions.");
                GD.Print("PASS: exported up-front block and inherited Weak/Vulnerable descriptions in both languages.");
                GD.Print("PASS: exported frost storage and 50% Hunted status descriptions in both languages.");
                GD.Print("PASS: exported Snowball Toss portrait/name and supply, Close the Net and Dark Harvest descriptions in both languages.");
                GD.Print("PASS: exported turn-limited Chain Claw, inherited-effect and free-use counter descriptions in both languages.");
                var surprisePortrait = GD.Load<Texture2D>("res://images/packed/card_portraits/lingdawnpao/dam_break.png");
                if (surprisePortrait == null) throw new Exception("Surprise Attack's reused Sneak Attack portrait is missing");
            }
            Assembly.Load("AmbushRework").EntryPoint!.Invoke(null, new object[] { Array.Empty<string>() });
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError((exception is TargetInvocationException ? exception.InnerException : exception)?.ToString());
            GetTree().Quit(1);
        }
    }

    private static bool ChoicePromptStub(ref MegaCrit.Sts2.Core.Localization.LocString __result)
    {
        __result = new MegaCrit.Sts2.Core.Localization.LocString("cards", "headless-choice");
        return false;
    }
}
