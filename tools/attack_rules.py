"""Attack-specific application stages, resolved against the pinned weapon module."""
def compile_attacks(weapon):
    raw=weapon['raw'];name=weapon['englishName'];result=[]
    for attack in raw.get('Attacks',[]):
        key=attack['AttackName'];radial=attack.get('ShotType')=='AoE'
        heavy='Heavy' in key or 'Recall Explosion' in key
        rule=dict(key=key,damageSource='Melee',co=0 if radial else 1,coMultiplicative=False,heavy=heavy,
                  heavyAnimationOnly=False,heavyDamageAnimationOnly=False,tennokai=True,incarnon=False,evolutionDamage=key in ['Normal Attack','Slam Attack','Heavy Slam Attack'],evolutionStats=True,
                  statusMods=True,criticalMods=True,elements=True,faction=True,ips=True,combo=heavy,code='',notes=[])
        if raw.get('Class')=='Glaive' and radial and 'Explosion' in key:rule['hitCombo']=False
        if key.startswith('Spectral ') or key=='Dagger Projectile':rule.update(incarnon=True)
        if key.startswith('Spectral '):rule.update(damageSource='Universal',evolutionDamage=False)
        if name in ['Ceramic Dagger','Okina','Okina Prime'] and key.startswith('Spectral ') and not radial:rule['coMultiplicative']=True
        if name in ['Okina','Okina Prime'] and key.startswith('Spectral '):rule.update(evolutionDamage=True,hitCombo=False,followThrough=False,tennokai=False,code='OkinaExplosion' if radial else 'OkinaDagger')
        if name=='Ack & Brunt' and key.startswith('Spectral '):rule.update(combo=False,code='SpectralAxe',coMultiplicative=not radial,hitCombo=False,followThrough=False,tennokai=False)
        if name=='Ceramic Dagger' and key.startswith('Spectral '):rule.update(heavy=True,combo=True,code='CeramicProjectile')
        if name in ['Caustacyst','Coda Caustacyst']:
            if key=='Corrosive Wave':rule.update(damageSource='Universal',followThrough=False,code='CaustacystWave')
            if key=='Corrosive Pool':rule.update(co=0,statusMods=False,criticalMods=False,followThrough=False,hitCombo=False,code='CaustacystPool')
        if name in ['Corufell','Corufell Prime'] and key=='Charged Projectile':rule.update(heavy=True,combo=True)
        if name in ['Corufell','Corufell Prime'] and key=='Charged Projectile':rule.update(coMultiplicative=True)
        if name=='Argo & Vel' and key=='Heavy Attack Glaive':rule.update(co=1/3,coCombo=False)
        if name=='Arum Spinosa' and 'Toxic Spines' in key:rule.update(co=.2 if key.startswith('First') else .1,coCombo=False)
        if name in ['Quassus','Quassus Prime'] and 'Ethereal Daggers' in key:rule.update(co=.4 if key.startswith('First') else .2,coCombo=False)
        if name=='Edun' and key.startswith('Polearm '):rule.update(heavy=True,combo=True,heavyAnimationOnly=True,tennokai=False,co=.25 if not radial else 0)
        if name=='Syam' and 'Shockwave' in key:rule.update(heavy=True,combo=True,heavyDamageAnimationOnly=True,coMultiplicative=True)
        if name=='Tenet Grigori' and key=='Energy Disk':rule.update(damageSource='Universal',coMultiplicative=True,heavy=True,combo=True)
        if name=='Tenet Agendus' and key=='Energy Disk':rule.update(heavy=True,combo=True,code='AgendusDisk',co=.2,coMultiplicative=True)
        if name=='Stropha' and key=='Ranged Attack':rule.update(rangeProfile='Stropha')
        if name in ['Sarpa','Vastilok'] and key=='Ranged Attack':rule.update(physicalMultiplier=1.25)
        if name=='Nepheri' and key=='Fireballs':rule.update(code='NepheriFireball',coMultiplicative=True)
        if name in ['Tatsu','Tatsu Prime'] and key=='Soul Swarm Projectile':rule.update(damageSource='Universal')
        if name=='Verdilac' and key=='Energy Wave':rule.update(coMultiplicative=True)
        if name=='Vitrica' and key=='Glass Explosion':rule.update(code='VitricaGlass',statusMods=False)
        if name=='Innodem' and key=='Dagger Projectile':rule.update(code='InnodemProjectile',co=2,coMultiplicative=False)
        if name=='Zenistar' and key.startswith('Disc '):
            # The CO table's 75-damage "Heavy Disc" is the contact, NOT
            # the 50-damage aura. Its contact result conflicts with the
            # weapon article; never silently use either as verified.
            rule.update(co=0,heavy=False,combo=False,tennokai=False,
                        followThrough=False,hitCombo=False,
                        radiusMods=key!='Disc Impact',healingReturn=key!='Disc Aura',
                        code='Zenistar'+key.removeprefix('Disc '))
            if key=='Disc Impact':
                rule['coSourceConflict']='제니스타 원반 접촉의 컨오: 무기 문서는 미적용, CO 계산표는 기본 피해 75에 보정 1040%로 서로 다릅니다. 지속 오라에 이 값을 적용하지 않습니다. Zenistar rev.2793361 / Condition Overload (Mechanic) rev.2815998'
            if key=='Disc Aura':
                rule['notes']=['원반 오라: 컨오·강공격 피해·콤보 피해·명중 콤보·힐링 리턴 제외. 사거리 및 공속 모드는 적용됩니다. 본문 주기 0.8초와 무기 자료의 초당 1.2회가 달라 자동 주기는 확정하지 않으며 현재 반복 간격은 사용자 가정입니다.']
        result.append(rule)
    # Heavy gunblade attacks use the gun's elements and per-pellet statistics.
    if raw.get('HeavyAttack',0)>0:result.append(dict(key='Heavy Attack',damageSource='Melee',co=.25 if name=='Stropha' else 1,heavy=True,combo=True,evolutionDamage=True,evolutionStats=True,tennokai=True,code='GunbladeHeavy' if raw.get('Class')=='Gunblade' else '',rangeProfile='Stropha' if name=='Stropha' else '',physicalMultiplier=1.25 if name in ['Sarpa','Vastilok'] else 1))
    if raw.get('SlideAttack',0)>0:result.append(dict(key='Slide Attack',damageSource='Melee',co=1,evolutionDamage=True,evolutionStats=True,tennokai=True))
    return result
