"""Self-test of check-r8-monitor.py: synchronized (lock) { call() } passes, the same method with its catch-all swapped
for a typed handler (the shape ART rejects) fails, and an empty dump fails rather than passing on nothing read."""
import pathlib
import subprocess
import sys
import tempfile

TOOL = pathlib.Path(__file__).with_name("check-r8-monitor.py")

SYNCHRONIZED = """\
0001d4:                                        |[0001d4] a.b.c:()V
0001e4: 1d01                                   |0000: monitor-enter v1
0001e6: 7100 0200 0000                         |0001: invoke-static {}, La/b;.d:()V // method@0002
0001ec: 1e01                                   |0004: monitor-exit v1
0001ee: 0e00                                   |0005: return-void
0001f0: 0d00                                   |0006: move-exception v0
0001f2: 1e01                                   |0007: monitor-exit v1
0001f4: 2700                                   |0008: throw v0
      catches       : 1
        0x0001 - 0x0005
          <any> -> 0x0006
      positions     :
"""


def run(listing):
    with tempfile.TemporaryDirectory() as scratch:
        dump = pathlib.Path(scratch, "dump.txt")
        dump.write_text(listing)
        return subprocess.run([sys.executable, "-I", str(TOOL), str(dump)], capture_output=True, text=True)


kept, typed, empty = run(SYNCHRONIZED), run(SYNCHRONIZED.replace("<any> ->", "Ljava/lang/Exception; ->")), run("")
assert kept.returncode == 0 and "with monitor-enter 1, would be rejected 0" in kept.stdout, kept.stdout + kept.stderr
assert typed.returncode == 1 and "REJECT a.b.c:()V" in typed.stdout, typed.stdout + typed.stderr
assert empty.returncode == 1, empty.stdout + empty.stderr
print("check-r8-monitor: self-test passed")
