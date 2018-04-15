
# WCLUtility
A utility to help with the [WCL](https://battlefy.com/world-of-tanks-community-league-wcl) - World of Tanks Communit League

## What?

It's a small console application, running over .Net 4.7.1, that receives a master file, from [Battlefy](https://battlefy.com/), containing the teams (clans), it's players, contact information etc and (try) to validade this data to ensure that:

* Clan's tags are correct;
* Player's Gamers Tags are correct;
* Contact's Gamer Tag is correct;
* Contact's e-mail os correct;
* Preferred and Backup Game Server are valid choices;
* The parent clan, if any, is correct;
* That players on the team that were on the top 32 teams of the previous season are correctly reported.

## How?

The program parses the CSV, and then for each peace of information it queries some [WG APIs](https://developers.wargaming.net/) and apply some heuristics to *clean up* the data.

The output of the program is another CSV, with the data validated, and aditional columns with normalized information, or the reason why the original record is wrong.

### Er... I mean *how to run* the program!

If you already have the binaries provided by me, just go to the directory where you copied this and click on the program `WCLUtilityGui.exe` (the one with the tank icon), and follow the
instructions.

The program may take several minutes to run, as it needs to query *a lot* of data over the Internet. So, be patient.

After the program runs it outputs 3 files, on the same directory that the original was:

* One with `valid.` before the original file name, containing general information about a player;
* One with `tanks.` before the original file name, containg details on every tier X tank that each player has ever played;
* One with `clans.` before the original file name, containg a summary of every team.

## Compiling

If you want to compile, just clone and open on Visual Studio (I used VS 2017, But the free edition will do it). You will be missing a small text file called `AppId.txt`. It contains the *Wargaming App Id* that you can create on their [Developer Portal](https://developers.wargaming.net/).

Without this key file on the same directory of the binaries the application will run with the `demo` app id, witch is quite limited, or may not even work at all.

## WCL3 Fields

### Full File

* Team Name;
* Gamer Tag (inGameName);
* CheckInAt (?)
* Team Name (Again!?);
* Clan Tag;
* Clan Membership Page Link;
* Preferred Server;
* Alternate Server;
* Team Contact E-Mail.

or the program can proccess a simple file, as bellow.

### Simple File

* Gamer Tag (inGameName);
* Team Name;
* Clan Tag.

## Path Ahead

* ~~Computing WN8, WinRate and Average Tier of the Players and Teams;~~
* ~~Exporting information on the tier X tanks;~~
* Exporting an Excel Workbook with clean records, and information on the tanks.
