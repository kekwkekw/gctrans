"""The user-supplied policy is a frozen input, not a rule generator."""
from hashlib import sha256
from pathlib import Path
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[1]
POLICY_SHA256 = "3aca17a2f12c76ca9c17d939a8f68e769b1eeefe54c8b52f6912d5f73f77922e"


class FrozenHoldPolicyTests(unittest.TestCase):
    def test_exact_uploaded_bytes(self):
        raw = (ROOT / "translation/HOLD_POLICY.md").read_bytes()
        self.assertEqual(2094, len(raw))
        self.assertEqual(POLICY_SHA256, sha256(raw).hexdigest())

    def test_git_does_not_rewrite_policy_newlines(self):
        if not (ROOT / ".git").exists():
            self.skipTest("Git attribute resolution requires a checkout")
        result = subprocess.run(
            ["git", "check-attr", "text", "--", "translation/HOLD_POLICY.md"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        self.assertEqual("translation/HOLD_POLICY.md: text: unset", result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
