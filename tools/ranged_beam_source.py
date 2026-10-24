"""Extract current beam ramp statements, excluding historical patch notes."""
import re


def beam_rule(text):
    for line in text.splitlines():
        match = re.search(r'Damage ramps up from (\d+(?:\.\d+)?)%.*?to 100% over (\d+(?:\.\d+)?) seconds', line, re.I)
        if match:
            return {'beamStart': float(match[1]) / 100,
                    'beamRampSeconds': float(match[2])}
    return None
