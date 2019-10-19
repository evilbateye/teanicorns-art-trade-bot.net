<p align="center">
    <img src="https://cdn.discordapp.com/avatars/568733620383121408/47178cf7aded1cd84daffcaea33981bd.png?size=256" style="border-radius: 28%" alt="Haruna Logo" height="128px" width="128px"></img>
</p>

# Teanicorns Art Trade BOT

Written in Discord.Net (2.0.1) for the needs of [Teanicorns](https://discord.gg/TS9SZYB) discord server, used to better organize art trade events.

## Usage

One event takes about a month and it is separated into an `entry week` and a `trade month`.
During entry week the trade participants register their art entries with the bot.
Their entry consists of either a `description` and/or an `image` file reference.

To set only the description use write the description text after the set command.

To set an image reference upload an image to the discord channel, and write the set command as the image comment.
The description is optional in this case.

When entry week ends, and all of the participants have registered their entries, admin closes the entry week and starts the trade month.
During this period the registered entries cannot be modified, but you can still see your entry using the get command.

<small><b>Note:</b>  Also during this period, no new entries may be registered. </small>

During the trade month trade participants have a new command available.
They can see the entry of their assigned art trade partner using the show command.

<small><b>Note:</b>  This is a secret, so the output is sent in a DM. </small>

The admin may shuffle or cherry pick the trade partners if they are unsatisfied with the random partners they got.
The list of commands expands over time, use the about command to get the most accurate list of your teanicorn bot version.

## Command list:
about | a : Show info about this bot.
set entry | se : Set your trade entry. (entry week only)
get entry | ge : Get your trade entry.
delete entry | de : Remove your trade entry. (entry week only)
show partner | sp : Sends you your trade partner's entry in a DM. (trade month only)
reveal art | ra : Registers your finished art, sends DM with the art to your trade partner. (trade month only)

## Admin only commands:
entry week | ew : Stops the art trade, clears all entries and theme, starts accepting entries.
trade month | tm : Starts the art trade, shuffles entries, sends all partners in a DM, stops accepting entries.
theme | th : Set the art trade theme.
start trade | st : Turns on/off the art trade (silent), sets start date to now, sets number of days until end.
channel | ch : Sets the working channel for ATB.
list | l : Sends you a list of all entries in a DM.
clear | cl : Delete all art trade entries.
shuffle | sf : Randomly shuffle art trade entries.
swap | sw : Changes your art trade partner.
restore | rs : Restores art trade entries from backup file / embeded JSON file.
backup | bp : Update backup file / flush entire ATB database in a DM as a JSON file.
history | hi : Show entire ATB database history in a DM as a JSON file.
send partners | sps : Send to all participants their trade partner's entry in a DM.

