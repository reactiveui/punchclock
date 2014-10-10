MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool
MONO ?= /usr/bin/env mono

.PHONY: all clean

all: Punchclock.nupkg

Punchclock.dll:
	$(MDTOOL) build -c:Release Punchclock.sln

Punchclock.nupkg: Punchclock.dll
	$(MONO) ./.nuget/NuGet.exe pack ./Punchclock.nuspec

clean:
	$(MDTOOL) build -t:Clean -c:Release Punchclock.sln
