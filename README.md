
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

If you already have the binaries provided by me, just go to the directory where you copied this and execute with the command

`WCLUtility Validate "c:\a path\Battlefy_master_file.csv"`

The program may take several minutes to run, as it needs to query *a lot* of data over the Internet. So, be patient.

When the program finishes it will write, on the same folder where the input file was, a new file with *valid.* before the original name. In our example it would be `c:\a path\valid.Battlefy_master_file.csv`.

The program will write a log file on the path `%LocalAppData%\Negri\WCL\Log`. It will also write a lot of small cache files on `%temp%`. You may want to delete these after the season.

## Compiling

If you want to compile, just clone and open on Visual Studio (I used VS 2017). You will be missing a small text file called `AppId.txt`. It contains the *Wargaming App Id* that you can create on their [Developer Portal](https://developers.wargaming.net/).

Without this key file on the same directory of the binaries the application will run with the `demo` app id, witch is quite limited.

## WCL3 Fields

* Team Name;
* Gamer Tag (inGameName);
* CheckInAt (?)
* Team Name (Again!?);
* Clan Tag;
* Clan Membership Page Link;
* Preferred Server;
* Alternate Server;
* Team Contact E-Mail.

## Path Ahead

* Computing WN8, WinRate and Average Tier of the Players and Teams;
