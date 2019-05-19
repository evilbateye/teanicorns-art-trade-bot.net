<p align="center">
    <img src="https://cdn.discordapp.com/avatars/568733620383121408/47178cf7aded1cd84daffcaea33981bd.png?size=256" style="border-radius: 28%" alt="Haruna Logo" height="128px" width="128px"></img>
</p>

# Teanicorns Art Trade BOT

Written in Discord.Net (2.0.1) for the needs of [Teanicorns](https://discord.gg/TS9SZYB) discord server, used to better organize art trade events.

## Usage

One event takes about a month and it is separated into an `entry week` and a `trade month`.
During entry week the trade participants register their art entries with the bot using the `.set entry` command.
Their entry consists of either a `description` and/or an `image` file reference.

To set only the description use `.set entry [description]`.

To set an image reference upload an image to the discord channel, and write `.set entry` as the image comment.
The description is optional in this case.

When entry week ends, and all of the participants have registered their entries, admin closes the entry week and starts the trade month using `.admin trade month`.
During this period the registered entries cannot be modified, but you can still see your entry using the `.get entry` command.

<small><b>Note:</b>  Also during this period, no new entries may be registered. </small>

During the trade month trade participants have a new command available.
You can see the entry of your assigned art trade partner using the `.show partner` command.

<small><b>Note:</b>  This is a secret, so the output is sent to you in a DM. </small>

The admin may shuffle or cherry pick the trade partners if they are unsatisfied with the random partners they got.
`.admin shuffle` command randomly shuffles all entries and `.admin swap [user1] [user2]` command changes the partner of user2 to user1.

## Command List

| Command | Description |
|--------------|-------------|
| .about | Show info about this bot. |
| .set entry | Set your trade entry. `[during entry week only]` |
| .get entry | Get your trade entry. |
| .delete entry | Remove your trade entry. `[during entry week only]` |
| .show partner | Sends you your trade partner's entry in a `DM`. `[during trade month only]` |
| .admin entry week | Stops the art trade, starts accepting entries. |
| .admin trade month | Starts the art trade, stops accepting entries. `[updates backup]` |
| .admin shuffle | Randomly shuffle art trade entries. |
| .admin clear | Delete all art trade entries. `[updates backup]` |
| .admin restore | Restores art trade entries from backup file. |
| .admin swap | Changes your art trade partner. |
| .admin list | Sends you a list of all entries in a `DM`. |
| .admin work channel | Sets the working channel for ATB. |

