# Village Intel

A Mount & Blade II: Bannerlord mod (v1.4.8) that lets you ask village notables what they've heard about enemy war parties, bandit gangs and bandit hideouts nearby. They won't always know, they won't always tell the truth, and they'll usually want paying.

## In game

Talk to a headman or rural notable in a village and choose **"What news is there of trouble around here?"**. There are three separate questions, and each one is paid for separately:

- **Enemy war parties**: lord parties, armies and patrols of any realm at war with you.
- **Bandits on the roads**: roaming bandit gangs.
- **Bandit camps**: infested hideouts.

The notable first gives a short answer listing what they know of, nearest first, as the kind of party and roughly how many men. For example: *I've seen an army of about 300 men, a patrol of about 30 men, and a lord's war party of about 60 men.*

You can then ask about each one (*"What about the army of about 300 men?"*) to hear whose it is, which village, castle or town it was seen near, and where it was going. For example: *That's Caladog's army of Battania. They were seen near Marunath, on their way to raid Seonon.* For a hideout, they tell you which settlement it's nearest to. These follow-up questions are free once you've paid for the answer.

### Will they know?

Each party or hideout in range is rolled separately. Something on the village's doorstep is almost always known (95%), and something at the edge of the radius only sometimes (25%). Hideouts don't move, so they're easier to know about, and armies are hard to miss.

### Will they tell the truth?

For each sighting they know about, the notable compares how much they like **you** with how much they like **that party**:

- Their relation with the party's leader.
- **+30** if the party belongs to the village's own realm, since they protect their own lords.
- **−20** if the party's realm is at war with the village's realm.
- For bandits: villagers start at −20 towards them, and each level of negative Honor makes a notable 25 points friendlier, so a crooked headman may be in league with the gang.

The lie chance is `15% − 0.5% × (your relation − their relation with the party)`, capped at 90%. Negative Honor adds 10% per level and positive Honor takes away 5% per level.

A lying notable either leaves the sighting out entirely, or sends you the wrong way: they name a different settlement in the area as where the party was seen and where it was heading, and get the numbers wrong, saying the band is half its real size or twice as big.

### Catching a liar

When a notable lies, your **Charm**, **Scouting** and **Roguery** are compared with theirs. If any of them is higher, you notice, and you get a new reply that calls them out. The wording depends on the skill with the biggest lead:

- **Charm**: *"You won't even meet my eye, and that smile came a moment too late…"*
- **Scouting**: *"That's not what the tracks on these roads tell me. I know this country…"*
- **Roguery**: *"That came out a little too smooth, like a story you'd rehearsed…"*

If you ask about a sighting they lied about, the reply changes to *"There it is again. You can't even keep your story straight…"* Noticing a lie gives a little experience in that skill.

You can press once per question:

- **Success**: the notable caves and gives the real answer, including anything they'd left out. You can then ask about each sighting again, and you get more skill experience.
- **Failure**: they're insulted, you lose 3 relation, and the answer stands.

The chance of success starts at 40%. Each point of your skill lead and each point of relation adds 0.4%, and bad relation subtracts. The chance is capped between 5% and 95%.

### What does it cost?

- At relation **−30 or lower**, they refuse to talk.
- At relation **+30 or higher**, they tell you for free.
- In between, they explain why talking is dangerous for them (the lords, or the bandits, would make them pay for it) and hint that something could change their mind. They never name a sum, but your reply shows what you're offering. Base prices at 0 relation are 100 denars for enemy parties, 60 for bandit gangs and 120 for hideouts. Bad relation raises the price, good relation lowers it, stingy notables charge more, and your Charm skill gets you up to a 40% discount.
- Sometimes they'd rather have **goods**: food or trade goods from your inventory worth about the price. Some will take either gold or goods, and some won't touch your coin at all.
- You can try to **talk them round** instead, using the game's normal persuasion screen with its success chances and progress. You get one try per question:
  - **Charm**: a friend is worth more than coin.
  - **Leadership**: I'll deal with them myself.
  - **Roguery**: nobody will know it came from you.

  It's easier the better they like you. If it works, the answer is free and you get skill experience for each argument that landed, the same way the game's own persuasions give it. If it fails, you can still pay.
- An honest notable who knows nothing tells you so for free. A dishonest one takes your money anyway.
- Paying gives +1 relation. The gold goes to the notable.

If you ask the same notable the same question within 24 hours, you get the same answer, so you can't reroll it. If you already paid, the repeat is free.

## Settings

All the numbers above can be changed in **Mod Options → Village Intel** (MCM). The same page has options to allow town notables, and to mark the map when a notable tells the truth about a hideout (off by default, because it gives the liars away).

### Debugging

There are debug switches for troubleshooting at the bottom of `src/IntelSettings.cs`. They aren't in the mod menu. Set them to `true` and rebuild:

- `DebugMode`: after each answer, the chat shows every party or hideout the notable was asked about, together with what was really there (size, nearest place, destination), the chance rolls, and one of these results:
  - **TRUTH**
  - **LIED**, with what they said instead
  - **LIED, left it out**
  - **DIDN'T KNOW**
  - **LIED, then CONFESSED**

  It also shows the skill comparison for noticing lies and the pressing roll, and says whether each follow-up answer was true. Notables still ask to be paid, but paying costs nothing: the chat shows what you would have been charged and the relation you would have gained.
- `DebugAlwaysKnowFlag`: notables know about everything in range, so only the lie rolls are random. Only works with `DebugMode` on.
- `DebugNoCooldownFlag`: every question gets a fresh roll. Only works with `DebugMode` on.

Keep them `false` for releases.

## Translating

The mod uses Bannerlord's standard localization. Every line of dialogue, every chat message and every Mod Options label has a `{=vi_...}` text id. The English text and the ids are listed in `ModuleData/Languages/std_VillageIntel.xml`.

To add a language (German shown here), put two files in a new language folder, either in this mod or in a separate translation mod:

`ModuleData/Languages/DE/language_data.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData id="Deutsch">
  <LanguageFile xml_path="DE/std_VillageIntel_deu.xml" />
</LanguageData>
```

`ModuleData/Languages/DE/std_VillageIntel_deu.xml` is a copy of `std_VillageIntel.xml` with `<tag language="Deutsch" />` and each `text` translated. Keep the ids and every `{VARIABLE}` as they are. The `id` must match the game's own name for the language, which is the `LanguageData id` used in the game's language folders (`Deutsch`, `Français`, `Türkçe`, `Русский`, ...).

Settlement, party, faction and item names come from the game, so they're already translated.

## Requirements

Harmony, ButterLib, UIExtenderEx and MCM (Mod Configuration Menu).

## Building

```
cd src
dotnet build -c Release
```

The build installs the mod into `Modules\VillageIntel` in the game folder. If Bannerlord isn't in the default Steam location, pass `/p:GameFolder="..."`.

Nothing is written to your save: the "asked recently" memory resets when you load a game, so the mod can be added or removed mid-campaign.
