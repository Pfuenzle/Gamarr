type ColonReplacementFormat =
  'delete' | 'dash' | 'spaceDash' | 'spaceDashSpace' | 'smart';

type RenameProfile =
  'gamarr' | 'noIntroPreserveById' | 'noIntroCanonical' | 'switchTitleDb';

export default interface NamingConfig {
  renameGames: boolean;
  renameProfile: RenameProfile;
  enableSwitchTitleDbRename: boolean;
  replaceIllegalCharacters: boolean;
  colonReplacementFormat: ColonReplacementFormat;
  standardGameFormat: string;
  gameFolderFormat: string;
}
