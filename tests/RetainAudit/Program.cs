using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.TestSupport;
using lingdawnpao.Core;
TestMode.IsOn = true;
foreach (var t in typeof(CardModel).Assembly.GetTypes().Where(t => !t.IsAbstract
    && (t.IsSubclassOf(typeof(CardModel)) || t.IsSubclassOf(typeof(CardPoolModel)) || t.IsSubclassOf(typeof(PowerModel)))))
    ModelDb.Inject(t);
foreach (var t in typeof(Lingdawnpao).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(CardModel)))) ModelDb.Inject(t);
ModelDb.Inject(typeof(LingdawnpaoCardPool));
var pools = new CardPoolModel[] { ModelDb.CardPool<IroncladCardPool>(), ModelDb.CardPool<SilentCardPool>(),
 ModelDb.CardPool<DefectCardPool>(), ModelDb.CardPool<RegentCardPool>(), ModelDb.CardPool<NecrobinderCardPool>(),
 ModelDb.CardPool<LingdawnpaoCardPool>(), ModelDb.CardPool<TokenCardPool>(),
 ModelDb.CardPool<ColorlessCardPool>(), ModelDb.CardPool<EventCardPool>() };
var report = new List<object>();
foreach (var pool in pools)
{
 var rows = new List<object>();
 foreach (var card in pool.AllCards)
 {
  bool before = card.Keywords.Contains(CardKeyword.Retain);
  var mutable = card.ToMutable();
  string? error = null;
  try { mutable.UpgradeInternal(); } catch(Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
  rows.Add(new { type = card.GetType().Name, id = card.Id.Entry, rarity = card.Rarity.ToString(),
   multiplayer = card.MultiplayerConstraint.ToString(), before, after = mutable.Keywords.Contains(CardKeyword.Retain), error });
 }
 report.Add(new { pool = pool.GetType().Name, rows });
}
var text = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("build/retain-audit.json", text);
Console.WriteLine("Audited " + pools.Length + " actual card pools; results in build/retain-audit.json");
