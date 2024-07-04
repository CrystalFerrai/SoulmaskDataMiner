Command line program to extra data from the game Soulmask. This is mainly just for my own use to update data each week when the game updates.

## Usage

Run the program with no parameters to print the usage.
```
Usage: SoulmaskDataMiner [[options]] [game assets directory] [output directory]

  [game assets directory]  Path to a directory containing .pak files for a game.

  [output directory]       Directory to output exported assets.

Options

  --key [key]       The AES encryption key for the game's data.

  --miners [miners] Comma separated list of miners to run. If not specified,
                    default miners will run.
```

This will also print a list of the names of all available miners.

## Releases

There are no releases of this tool for the time being. If you wish to try it, you will need to build it.

## Building

Clone the repository, including submodules.
```
git clone --recursive https://github.com/CrystalFerrai/SoulmaskDataMiner.git
```

You can then open and build SoulmaskDataMiner.sln.
