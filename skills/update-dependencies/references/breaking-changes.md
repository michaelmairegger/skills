# Major Updates von `Angular` `.NET`

- Aktualisierungen sollen auf eigenen Branches (`migrate/<Typ>/<Major>`) durchgeführt werden, welche abschließend in den aktuellen Branch gemerged werden.
- Sind bei beiden Technologien Updates notwending werden diese mit einen Oktobus-merge gemerged.

# Breaking Changes

- Wenn ein Major-Versionssprung eines Paketes durchgeführt wird, soll in den release-notes des jeweiligen Paketes nach Breaking Changes gesucht werden. Bei Breaking Changes soll die Codebasis nach möglichen Stellen durchsucht werden, die betroffen sein könnten. In diesem Fall soll versucht werden, die Codebasis so anzupassen, sodass sie mit der neuen Major-Version kompatibel ist. Wenn dies nicht möglich ist, soll der Benutzer darauf hingewiesen werden, dass die Aktualisierung für dieses Paket nicht durchgeführt werden kann
- Es sollen dann die Codestellen in einer Tabelle hervorgeheben werden, welche von den Breaking Changes betroffen sein könnten. Die Tabelle soll die Codestelle, die Art des Breaking Changes und eine kurze Beschreibung enthalten. Der Benutzer soll dann entscheiden, ob er die Codestellen anpassen möchte oder nicht. Wenn der Benutzer entscheidet, dass er die Codestellen nicht anpassen möchte, so soll der Benutzer darauf hingewiesen werden, dass die Aktualisierung für dieses Paket nicht durchgeführt werden kann.