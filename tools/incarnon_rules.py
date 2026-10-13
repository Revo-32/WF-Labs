"""Reviewed numeric evolution rules. Names are resolved to internal IDs at import time."""
import re

def compile_incarnons(weapons, pages):
    output=[]
    def family(names,form,stages):
        names=names.split('|')
        for variant,name in enumerate(names):
            w=next(w for w in weapons if w['englishName']==name and w['category']=='melee')
            title=names[0]+' Incarnon Genesis' if names[0]+' Incarnon Genesis' in pages else names[0]
            p=pages[title]
            def scalar(d):return {k:(v[variant] if isinstance(v,list) else v) for k,v in d.items()}
            perks=[]
            for stage,rows in enumerate(stages,2):
                for index,row in enumerate(rows):
                    label,effects,*rest=row
                    condition,cap,conditional=(rest+[None,0,{}])[:3] if rest else (None,0,{})
                    perks.append(dict(id=f'{w["internalId"]}/evolution/{stage}/{index+1}',stage=stage,name=label,effects=scalar(effects),condition=condition,cap=cap,conditional=scalar(conditional)))
            code=re.sub(r'[^A-Za-z]','',names[0])
            output.append(dict(weaponId=w['internalId'],code=code,form=scalar(form),perks=perks,source={k:v for k,v in p.items() if k!='content'}))
    family('Ack & Brunt',{'Damage':1},[
        [('Templar’s Wrath',{'BaseDamage':150},'Armor450',1,{'HeavyEfficiency':.7}),('Shieldmaiden’s Rush',{'BaseDamage':150},'ShieldBreak',1,{'BlockingComboChance':3})],
        [('Orokin Reach',{'Range':.7}),('Abiding Hold',{'HolsterPause':1}),('Swift Break',{'Windup':.7})],
        [('Rogue Readiness',{'KillReload':.5}),('Absolute Valor',{'BaseCritical':.17}),('Versatile Creed',{'BaseCritical':.09,'BaseStatus':.09})]])
    family('Anku',{'Damage':1},[
        [('Edge of Justice',{'BaseDamage':50},'Equipped',1,{'AttackSpeed':.4}),('Guardian’s Promise',{'BaseDamage':50},'Overshields',1,{'HeavyEfficiency':.8})],
        [('Standoff',{'HolsterPause':1}),('Swordsman’s Celerity',{'MovementSpeed':.2}),('Swift Break',{'Windup':.6})],
        [('Absolute Dominion',{'BaseStatus':.2}),('Subtle Force',{'BaseCritical':.06,'BaseStatus':.14}),('Absolute Valor',{'BaseCritical':.12})]])
    family('Bo|Mk1-Bo|Bo Prime',{'Damage':1,'Range':4,'HeavyEfficiency':.5},[
        [('Stalwart Oak',{'BaseDamage':[110,90,34]},'Armor450',1,{'BlockAngle':30,'BlockingComboChance':3}),('Swordsman’s Flourish',{'BaseDamage':[100,85,30]},'Equipped',1,{'ComboChance':1})],
        [('Orokin Reach',{'Range':.5}),('Overhand',{'HeavyEfficiency':.2}),('Swordsman’s Celerity',{'MovementSpeed':.2})],
        [('Flashing Bleed',{'ImpactToSlash':.4}),('Subtle Force',{'BaseCritical':[.12,.15,.06],'BaseStatus':[.12,.15,.06]}),('Absolute Dominion',{'BaseStatus':[.34,.38,.30]})]])
    family('Ceramic Dagger',{'Damage':1},[
        [('Gun and Blade',{'BaseDamage':100},'PrimaryKills',100,{'InitialCombo':1}),('Breacher’s Opportunity',{'BaseDamage':120},'ShieldBreak',1,{'BaseDamage':80})],
        [('Orokin Reach',{'Range':1}),('Adept Reflexes',{'InitialCombo':20}),('Rogue Readiness',{'KillReload':.4})],
        [('Red Right Hand',{},'FirstQuickMelee',1,{'BaseCriticalDamage':2}),('Absolute Dominion',{'BaseStatus':.3}),('Absolute Valor',{'BaseCritical':.3})]])
    family('Destreza|Destreza Prime',{'Damage':1},[
        [('Weighted Impetus',{'BaseDamage':[50,40]},'HeavyKill',1,{'Windup':1}),('Piercing Stature',{'BaseDamage':[50,40]},'PunctureStacks',5,{'StatusChance':.2})],
        [('Orokin Reach',{'Range':1}),('Overhand',{'HeavyEfficiency':.3}),('Adept Reflexes',{'InitialCombo':20})],
        [('Absolute Dominion',{'BaseStatus':[.20,.16]}),('Critical Forte',{'BaseCriticalDamage':[1.2,.6]}),('Decisive Stature',{'StatusToBaseCritical':.25})]])
    family('Dual Ichor',{'Damage':1},[
        [('Alchemist’s Wrath',{'BaseDamage':90,'ComboOnToxin':5}),('Ronin’s Rush',{'BaseDamage':90},'NeutralFinalHit',1,{'AttackSpeed':.3})],
        [('Orokin Reach',{'Range':.6}),('Resolute Force',{'ComboDuration':7}),('Swift Break',{'Windup':1})],
        [('Absolute Valor',{'BaseStatus':.34}),('Poison Parasite',{'ToxinKillHealing':33}),('Universal Readiness',{'AmmoCombo':5})]])
    family('Furax|Mk1-Furax|Furax Wraith',{'Damage':1,'HeavySlamEfficiency':.9},[
        [('Swordsman’s Flourish',{'BaseDamage':[110,100,65]},'Equipped',1,{'ComboChance':1}),('Kill Joy',{'BaseDamage':[90,80,55]},'NearbyEnemies',5,{'AttackSpeed':.1})],
        [('Moonrise Velocity',{'Range':1}),('Overhand',{'HeavyEfficiency':.2}),('Standoff',{'HolsterPause':1})],
        [('Flashing Bleed',{'ImpactToSlash':.4}),('Kinetic Killer',{},'Sliding',1,{'SetBaseCriticalDamage':4}),('Absolute Dominion',{'BaseStatus':[.24,.22,.14]})]])
    family('Hate',{'Damage':1},[
        [('Swordsman’s Flourish',{'BaseDamage':30},'Equipped',1,{'ComboChance':1}),('Stalker’s Legacy',{'BaseDamage':30},'DreadDespair',1,{'InitialCombo':30})],
        [('Orokin Reach',{'Range':.8}),('Swift Break',{'Windup':.6}),('Resolute Force',{'ComboDuration':10})],
        [('Absolute Valor',{'BaseCritical':.1}),('Absolute Dominion',{'BaseStatus':.2}),('Subtle Force',{'BaseCritical':.06,'BaseStatus':.10})]])
    family('Magistar|Sancti Magistar',{'Damage':1,'InitialCombo':30,'Windup':.5},[
        [('Crushing Verdict',{'BaseDamage':[100,20]},'Channeling',1,{'FollowThrough':.4}),('Edge Of Justice',{'BaseDamage':[100,20]},'Equipped',1,{'AttackSpeed':.4})],
        [('Seismic Slam',{'SlamRadiusPercent':1}),('Orokin Reach',{'Range':1.4}),('Swift Break',{'Windup':.3})],
        [('Flashing Bleed',{'ImpactToSlash':.5}),('Subtle Force',{'BaseCritical':.08,'BaseStatus':.08}),('Critical Parallel',{'BaseCritical':[.16,.12],'BaseCriticalDamage':[1,.2]})]])
    family('Nami Solo',{'Damage':1,'Range':3},[
        [('Brigand’s Frenzy',{'BaseDamage':80},'OnEquip',1,{'AttackSpeed':.3}),('Master’s Flourish',{'BaseDamage':80,'FinisherHealing':40})],
        [('Lone Blade',{},'Equipped',1,{'FollowThrough':.6}),('Swift Break',{'Windup':.5}),('Resolute Force',{'ComboDuration':6})],
        [('Absolute Valor',{'BaseCritical':.21}),('Versatile Creed',{'BaseCritical':.14,'BaseStatus':.14}),('Absolute Dominion',{'BaseStatus':.30})]])
    family('Obex|Prisma Obex',{'Damage':1,'FinisherSplashRadius':12},[
        [('Balanced Stagger',{'BaseDamage':[50,10],'NeutralFinisherOpenChance':.2}),('Armored Finisher',{'BaseDamage':[50,10]},'Armor450',1,{'FinisherDamage':.8})],
        [('Rapid Conclusion',{},'FinisherStacks',16,{'ParkourSpeed':.025}),('Orokin Reach',{'Range':1.2}),('Standoff',{'HolsterPause':1})],
        [('Subtle Force',{'BaseCritical':.1,'BaseStatus':[.18,.12]}),('Absolute Dominion',{'BaseStatus':[.28,.20]}),('Critical Coefficient',{'BaseCriticalDamage':.6,'IncarnonCriticalDamage':.6})]])
    family('Okina|Okina Prime',{'Damage':1},[
        [('Seeing Red',{'BaseDamage':[60,20],'ComboOnSlash':5}),('Synergist Surety',{'BaseDamage':[60,20]},'CriticalStacks',5,{'StatusDamage':[.08,.06]})],
        [('Orokin Reach',{'Range':1}),('Standoff',{'HolsterPause':1}),('Swordsman’s Celerity',{'MovementSpeed':.3})],
        [('Commodore’s Fortune',{'BaseCritical':[.12,.06]}),('Alchemy of War',{'BaseStatus':[.16,.12],'StatusDuration':.25}),('Survivor’s Edge',{'BaseCritical':[.06,.02],'BaseStatus':[.10,.08]})]])
    family('Sibear',{'Damage':1,'Windup':.5},[
        [('Master’s Shatter',{'BaseDamage':20,'ComboOnCold':10}),('Thane’s Wrath',{'BaseDamage':20},'Armor450',1,{'BaseDamage':40})],
        [('Orokin Reach',{'Range':1}),('Kinetic Harmony',{'Windup':.6}),('Resolute Force',{'ComboDuration':6})],
        [('Mounting Avalanche',{},'ColdKills',4,{'InitialCombo':15}),('Absolute Valor',{'BaseCritical':.25}),('Red Right Hand',{},'FirstQuickMelee',1,{'BaseCriticalDamage':2})]])
    family('Skana|Prisma Skana|Skana Prime',{'Damage':1,'TransformStunRadius':10},[
        [('Guardian’s Promise',{'BaseDamage':[100,100,10]},'Overshields',1,{'HeavyEfficiency':.8}),('Wartime Nerve',{'BaseDamage':[90,90,10]},'Undamaged',1,{'ExtraCombo':9})],
        [('Orokin Reach',{'Range':.4}),('Resolute Force',{'ComboDuration':6}),('Swift Break',{'Windup':.5})],
        [('Survivor’s Edge',{'BaseCritical':[.12,.12,.02],'BaseStatus':[.10,.10,.04]}),('Elemental Excess',{'BaseStatus':[.20,.20,.10]}),('Absolute Valor',{'BaseCritical':[.25,.25,.04]})]])
    family('Innodem',{'Range':3,'AttackSpeed':.4},[
        [('Orokin Reach',{'Range':.5}),('Bladed Harmony',{'AttackSpeed':.2}),('Striking Swiftness',{'SprintSpeed':.3})],
        [('Hawk Momentum',{'SlideDistanceCombo':5}),('Skyborne Hunter',{'AerialRange':.5}),('Blade Twister',{'FinisherDamage':.6})],
        [('Incarnon Imago',{'TransformCombo':3}),('Swooping Lunge',{},'AirborneKills',3,{'Damage':.5}),('Protracted Execution',{'FinisherCombo':20})],
        [('Blood Anointed',{},'FinisherBuff',1,{'HeavyEfficiency':.4}),('Stunning Brutality',{'FinisherStunRadius':10}),('Armed Inspiration',{'AmmoCombo':5})]])
    family('Praedos',{'Range':3,'AttackSpeed':.4},[
        [('Seismic Slam',{'SlamRadiusPercent':.5}),('Whirling Flurry',{'AttackSpeed':.2}),('Drifting Grace',{'SprintSpeed':.2,'Slide':.2})],
        [('Shockwave Synergy',{'SlamCombo':4}),('Reaching Lunge',{'SlideRange':1.5}),('Adept Reflexes',{'InitialCombo':20})],
        [('Swift Transmute',{'TransformCombo':3}),('Evolved Ascension',{'ParkourSpeed':.3}),('Vaulting Leap',{'DoubleJump':1})],
        [('Transfigured Momentum',{},'SlideKill',1,{'HeavyEfficiency':.5}),('Kinetic Harmony',{'Windup':1}),('Universal Readiness',{'AmmoCombo':5})]])
    family('Ruvox',{'Range':3,'SpeedMultiplier':-.35,'ImpactToPuncture':1},[
        [('Orokin Reach',{'Range':1}),('Lethal Impetus',{},'RuvoxKills',3,{'AttackSpeed':.15}),('Gathering Momentum',{'MovementPerCombo':.05})],
        [('Shockwave Synergy',{'SlamCombo':4}),('Seismic Slam',{'SlamRadiusPercent':.6}),('Adept Reflexes',{'InitialCombo':20})],
        [('Swift Transmute',{'TransformCombo':3}),('Ternary Vault',{'ExtraJump':1}),('Inspiring Execution',{},'FinisherBuff',1,{'ComboChance':.3})],
        [('Brutal Efficiency',{},'ImpaledFive',1,{'HeavyEfficiency':.4}),('Vulnerability Serum',{},'Impaled',1,{'FlatStatus':.35}),('Permanent Perforation',{},'Impaled',1,{'ForcedPunctureStacks':5})]])
    family('Thalys',{'Range':3,'AttackSpeed':.4},[
        [('Dreadful Reach',{},'ThalysStatus',20,{'Range':.1}),('Whirling Flurry',{'AttackSpeed':.2}),('Raging Drift',{'Slide':.2},'Sliding',1,{'AttackSpeed':.8})],
        [('Nimble Scythe',{'SlideCombo':5}),('Echoes of Rage',{'ShardCombo':1}),('Adept Reflexes',{'InitialCombo':20})],
        [('Swift Transmute',{'TransformCombo':3}),('Vaulting Leap',{'JumpHeight':1,'DoubleJump':1}),('Devastating Mercy',{'GroundFinisherKnockdownRadius':6})],
        [('Explosive Growth',{'ShardImpale':1}),('Chain Shatter',{'ShardDetonation':1}),('Void Splinters',{'ShardDuration':30,'ShardDamageMultiplier':-.5,'ShardWeakspotCritical':1})]])
    return output
