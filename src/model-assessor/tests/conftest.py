import sys
from pathlib import Path

MODEL_ASSESSOR_ROOT = Path(__file__).resolve().parents[1]
MODEL_ASSESSOR_ROOT_STR = str(MODEL_ASSESSOR_ROOT)
if MODEL_ASSESSOR_ROOT_STR not in sys.path:
    sys.path.insert(0, MODEL_ASSESSOR_ROOT_STR)
