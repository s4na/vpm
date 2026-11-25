import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class ReleaseTest(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(dir=Path.cwd())
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name) / "checkout"
        self.root.mkdir()
        self.script = Path(__file__).with_name("prepare-vpm-release.py").resolve()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Release Test")
        self.git("config", "user.email", "test@example.invalid")
        for name, version in (("com.s4na.utils", "0.1.6"),
                              ("com.s4na.avatar-essentials", "1.1.0"),
                              ("example.external", "2.1.0")):
            path = self.root / "Packages" / name / "package.json"
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps({"name": name, "version": version}), encoding="utf-8")
        self.git("add", ".")
        self.git("commit", "-m", "Initial package manifests")
        self.remote = self.root.parent / "remote.git"
        self.git("init", "--bare", str(self.remote))
        self.git("remote", "add", "origin", str(self.remote))
        self.git("push", "origin", "main")

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.root, text=True,
                                       stderr=subprocess.PIPE).strip()

    def release(self, *args):
        return subprocess.run([sys.executable, str(self.script), *args], cwd=self.root,
                              text=True, capture_output=True)

    def publish(self, *args):
        result = self.release(*args)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout.strip()

    def version(self, commit, name):
        return json.loads(self.git("show", f"{commit}:Packages/{name}/package.json"))["version"]

    def main_tip(self):
        return self.git("ls-remote", "origin", "refs/heads/main").split()[0]

    def test_publishes_exact_bumped_commit_and_retry_does_not_roll_back(self):
        first = self.main_tip()
        released = self.publish("--source", first)
        self.assertEqual(released, self.main_tip())
        self.assertEqual(self.version(released, "com.s4na.utils"), "0.1.7")
        self.assertEqual(self.version(released, "com.s4na.avatar-essentials"), "1.1.1")
        self.assertEqual(self.version(released, "example.external"), "2.1.0")
        self.assertEqual(self.publish("--source", first), released)
        self.git("commit", "--allow-empty", "-m", "Later merged change")
        self.git("push", "origin", "main")
        second = self.publish("--source", self.main_tip())
        self.assertEqual(self.version(second, "com.s4na.utils"), "0.1.8")
        self.assertEqual(self.publish("--source", first), second)
        self.assertEqual(self.publish(), second)
        self.assertEqual(self.main_tip(), second)

    def test_retries_when_another_merge_advances_main(self):
        source = self.main_tip()
        other = self.root.parent / "another-checkout"
        self.git("clone", "--branch", "main", str(self.remote), str(other))
        self.git("-C", str(other), "-c", "user.name=Release Test", "-c",
                 "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "Concurrent merge")
        self.git("-C", str(other), "push", "origin", "main")
        concurrent = self.main_tip()
        released = self.publish("--source", source)
        self.assertEqual(self.git("rev-parse", f"{released}^"), concurrent)
        self.assertEqual(self.main_tip(), released)
        self.assertEqual(self.version(released, "com.s4na.utils"), "0.1.7")

    def test_invalid_version_leaves_remote_unchanged(self):
        path = self.root / "Packages/com.s4na.utils/package.json"
        path.write_text('{"name":"com.s4na.utils","version":"broken"}', encoding="utf-8")
        self.git("add", ".")
        self.git("commit", "-m", "Invalid version")
        self.git("push", "origin", "main")
        original = self.main_tip()
        self.assertNotEqual(self.release("--source", original).returncode, 0)
        self.assertEqual(self.main_tip(), original)
        self.assertEqual(self.git("status", "--porcelain"), "")


if __name__ == "__main__":
    unittest.main()
