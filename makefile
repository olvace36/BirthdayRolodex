MOD_NAME=BirthdayRolodex
MODE?=GOG
GAME_DIR=${HOME}/GOG Games/Stardew Valley/game
ifeq (${MODE}, steam)
	GAME_DIR=${HOME}/.steam/steam/steamapps/common/Stardew Valley
endif
MOD_DIR=${GAME_DIR}/Mods/${MOD_NAME}

install: smapi

smapi:
	MOD_FOLDER_NAME=${MOD_NAME} MODE=${MODE} dotnet build
	install -m 644 LICENSE "${MOD_DIR}"

clean:
	rm -rf bin obj

uninstall:
	rm -rf "${MOD_DIR}"
