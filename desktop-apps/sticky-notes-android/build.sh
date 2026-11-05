#!/bin/bash
# Builds StickyNotes.apk (Sticky Notes for Android) into build/StickyNotes.apk.
# First checks the note file format (shared with the Windows and Mac apps) with the unit tests.
# Needs JDK 17, Gradle 8.10 and the Android SDK (ANDROID_HOME); GitHub's Ubuntu runners have them.
# Set STICKY_KEYSTORE and STICKY_KEYSTORE_PASSWORD to sign with the release key (see build.gradle);
# without them the APK is signed with the debug key, and the Google sign-in won't work.
set -euo pipefail
cd "$(dirname "$0")"
gradle --no-daemon --console=plain testReleaseUnitTest assembleRelease
mkdir -p build
cp build/outputs/apk/release/*.apk build/StickyNotes.apk
ls -l build/StickyNotes.apk
