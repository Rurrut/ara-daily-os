"""Build the offline Android app using a local JDK 17 and Android build-tools 35.
Usage: python build_android.py --jdk PATH --tools PATH --android-jar PATH
Keep the signing keystore for subsequent upgrades; never include it in the app ZIP.
"""
import argparse, os, shutil, subprocess, zipfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--jdk', required=True)
p.add_argument('--tools', required=True)
p.add_argument('--android-jar', required=True)
p.add_argument('--work', required=True)
p.add_argument('--keystore', required=True)
p.add_argument('--out')
p.add_argument('--edition', choices=['personal','preview'], default='personal')
a = p.parse_args()
root = Path(__file__).resolve().parent
out = Path(a.out).resolve() if a.out else root.parent / 'ARA-Daily-OS.apk'
work = Path(a.work).resolve()
work.mkdir(parents=True, exist_ok=True)
jdk, tools, jar = Path(a.jdk), Path(a.tools), Path(a.android_jar)
env = dict(os.environ, JAVA_HOME=str(jdk), PATH=str(jdk / 'bin') + os.pathsep + os.environ.get('PATH', ''))
def run(*args):
    subprocess.run([str(x) for x in args], check=True, env=env)
for name in ('classes', 'dex', 'assets', 'src', 'res'):
    (work / name).mkdir(exist_ok=True)
# Strip Windows UTF-8 BOMs from text inputs without altering delivered sources.
for src, dest in ((root / 'src', work / 'src'), (root / 'res', work / 'res')):
    for f in src.rglob('*'):
        if f.is_file():
            target = dest / f.relative_to(src); target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(f.read_text(encoding='utf-8-sig'), encoding='utf-8')
for name in ('index.html', 'mobile.css', 'mobile.js', 'offline-core.js'):
    (work / 'assets' / name).write_text((root.parent / 'mobile' / name).read_text(encoding='utf-8-sig'), encoding='utf-8')
shutil.copyfile(root / 'update-public.der', work / 'assets' / 'update-public.der')
manifest = work / 'AndroidManifest.xml'
manifest_text=(root / 'AndroidManifest.xml').read_text(encoding='utf-8-sig')
if a.edition=='preview':
    manifest_text=manifest_text.replace('package="ara.daily"','package="aiary.preview"').replace('android:versionCode="27" android:versionName="2.10.0"','android:versionCode="1" android:versionName="0.1.0"').replace('android:label="Aiary · 아이어리"','android:label="Aiary Preview"').replace('android:name=".','android:name="ara.daily.').replace('ara.daily.updates','aiary.preview.updates').replace('android:scheme="aradaily"','android:scheme="aiarypreview"')
    edition=work/'src/ara/daily/AppEdition.java'
    text=edition.read_text('utf-8').replace('PREVIEW=false','PREVIEW=true').replace('NATIVE=27','NATIVE=1').replace('VERSION="2.10.0"','VERSION="0.1.0"').replace('"ara-update.json"','"aiary-preview-update.json"').replace('"mobile-update.zip"','"mobile-preview-update.zip"').replace('"ARA-Daily-OS-Android-"','"Aiary-Preview-Android-"')
    edition.write_text(text,encoding='utf-8')
manifest.write_text(manifest_text, encoding='utf-8')
run(tools / 'aapt2.exe', 'compile', '--dir', work / 'res', '-o', work / 'resources.zip')
run(tools / 'aapt2.exe', 'link', '--java', work / 'src', '--custom-package', 'ara.daily', '-o', work / 'unsigned.apk', '--manifest', manifest, '-I', jar, '-A', work / 'assets', work / 'resources.zip')
qr_library = root / 'lib' / 'zxing-core-3.5.3.jar'
if not qr_library.exists(): raise FileNotFoundError('Bundled ZXing library is missing')
run(jdk / 'bin/javac.exe', '-encoding', 'UTF-8', '--release', '8', '-classpath', str(jar) + os.pathsep + str(qr_library), '-d', work / 'classes', *list((work / 'src').rglob('*.java')))
run(tools / 'd8.bat', '--lib', jar, '--min-api', '26', '--output', work / 'dex', *list((work / 'classes').rglob('*.class')), qr_library)
with zipfile.ZipFile(work / 'unsigned.apk', 'a', compression=zipfile.ZIP_DEFLATED) as z:
    for f in (work / 'dex').glob('*.dex'): z.write(f, f.name)
key = Path(a.keystore).resolve()
if not key.exists():
    key.parent.mkdir(parents=True, exist_ok=True)
    run(jdk / 'bin/keytool.exe', '-genkeypair', '-keystore', key, '-alias', 'aradaily', '-keyalg', 'RSA', '-keysize', '2048', '-validity', '10000', '-storepass', 'android', '-keypass', 'android', '-dname', 'CN=ARA Daily OS Local Build')
run(tools / 'zipalign.exe', '-p', '-f', '4', work / 'unsigned.apk', work / 'aligned.apk')
run(tools / 'apksigner.bat', 'sign', '--v4-signing-enabled', 'false', '--ks', key, '--ks-key-alias', 'aradaily', '--ks-pass', 'pass:android', '--key-pass', 'pass:android', '--out', out, work / 'aligned.apk')
run(tools / 'apksigner.bat', 'verify', '--verbose', out)
run(tools / 'aapt2.exe', 'dump', 'badging', out)
print('APK:', out)
