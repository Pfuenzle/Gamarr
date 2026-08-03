using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Games;
using NzbDrone.Core.Games.Components;
using NzbDrone.Core.Games.Translations;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RomCatalog;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Organizer
{
    [TestFixture]
    public class RenameProfileNamingBehaviorFixture : CoreTest<FileNameBuilder>
    {
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _namingConfig = NamingConfig.Default;
            _namingConfig.RenameGames = true;
            _namingConfig.RenameProfile = RenameProfile.Gamarr;

            Mocker.GetMock<INamingConfigService>()
                  .Setup(x => x.GetConfig())
                  .Returns(_namingConfig);

            Mocker.GetMock<IQualityDefinitionService>()
                  .Setup(x => x.Get(It.IsAny<Quality>()))
                  .Returns<Quality>(quality => Quality.DefaultQualityDefinitions.Single(x => x.Quality == quality));

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(x => x.All())
                  .Returns(new System.Collections.Generic.List<CustomFormat>());

            Mocker.GetMock<IGameTranslationService>()
                  .Setup(x => x.GetAllTranslationsForGameMetadata(It.IsAny<int>()))
                  .Returns(new System.Collections.Generic.List<GameTranslation>());
        }

        [Test]
        public void RenameProfile_should_preserve_existing_default_file_name_builder_output_for_normal_gamarr_profile()
        {
            var game = new Game
            {
                Title = "South Park",
                Year = 1998
            };

            var gameFile = new GameFile
            {
                Quality = new QualityModel(Quality.Uplay)
            };

            Subject.BuildFileName(game, gameFile)
                   .Should().Be("South Park (1998) Uplay");
        }

        [Test]
        public void RenameProfile_should_preserve_original_nointro_variant_filename_for_gamarr_profile()
        {
            var game = new Game
            {
                Id = 5,
                Title = "Mega Man IV",
                Year = 1993
            };

            var gameFile = new GameFile
            {
                GameId = 5,
                Quality = new QualityModel(Quality.Retail),
                OriginalFilePath = "Nintendo - Game Boy/Mega Man IV (USA).zip",
                RelativePath = "Mega Man IV (1993) Retail - Gamarr.zip"
            };

            Mocker.GetMock<IGameComponentRepository>()
                  .Setup(x => x.GetByGame(5))
                  .Returns(new System.Collections.Generic.List<GameComponent>
                  {
                      new GameComponent
                      {
                          Id = 11,
                          GameId = 5,
                          ComponentType = GameComponentType.NoIntroRetailRom,
                          Key = "nointro:retail:mega-man-iv-usa",
                          Title = "USA"
                      }
                  });

            Mocker.GetMock<INoIntroCatalogEntryRepository>()
                  .Setup(x => x.All())
                  .Returns(new[]
                  {
                      new NoIntroCatalogEntry
                      {
                          SystemKey = "nintendo---game-boy",
                          CanonicalName = "Mega Man IV (USA)",
                          CanonicalFileName = "Mega Man IV (USA).zip"
                      }
                  });

            Subject.BuildFileName(game, gameFile)
                   .Should().Be("Mega Man IV (USA)");
        }

        [Test]
        public void RenameProfile_should_use_switch_titledb_format_for_switch_base_game()
        {
            _namingConfig.EnableSwitchTitleDbRename = true;

            var game = new Game
            {
                Id = 6,
                Title = "Cult of the Lamb",
                Year = 2022,
                Platform = PlatformFamily.NintendoSwitch
            };

            var gameFile = new GameFile
            {
                GameId = 6,
                Quality = new QualityModel(Quality.Retail)
            };

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.FindBaseByTitles(It.Is<System.Collections.Generic.IEnumerable<string>>(titles => titles.Contains("Cult of the Lamb"))))
                  .Returns(new SwitchTitleDbMatch("Cult of the Lamb", "01002E7016C46000"));

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.GetLatestVersion("01002E7016C46000"))
                  .Returns(131072);

            Subject.BuildFileName(game, gameFile)
                   .Should().Be("Cult of the Lamb [01002E7016C46000][v131072]");
        }

        [Test]
        public void RenameProfile_should_use_switch_titledb_format_for_switch_update_component()
        {
            _namingConfig.EnableSwitchTitleDbRename = true;

            var game = new Game
            {
                Id = 7,
                Title = "Cult of the Lamb",
                Year = 2022,
                Platform = PlatformFamily.NintendoSwitch
            };

            var gameFile = new GameFile
            {
                GameId = 7,
                ComponentId = 21,
                Quality = new QualityModel(Quality.Retail)
            };

            Mocker.GetMock<IGameComponentRepository>()
                  .Setup(x => x.Get(21))
                  .Returns(new GameComponent
                  {
                      Id = 21,
                      GameId = 7,
                      ComponentType = GameComponentType.Update,
                      Key = "v1.2.3",
                      Title = "v1.2.3"
                  });

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.FindBaseByTitles(It.Is<System.Collections.Generic.IEnumerable<string>>(titles => titles.Contains("Cult of the Lamb"))))
                  .Returns(new SwitchTitleDbMatch("Cult of the Lamb", "01002E7016C46000"));

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.GetLatestVersion("01002E7016C46800"))
                  .Returns(1835008);

            Subject.BuildFileName(game, gameFile)
                   .Should().Be("Cult of the Lamb [01002E7016C46800][v1835008]");
        }

        [Test]
        public void RenameProfile_should_use_switch_titledb_format_for_switch_dlc_component()
        {
            _namingConfig.EnableSwitchTitleDbRename = true;

            var game = new Game
            {
                Id = 8,
                Title = "Cult of the Lamb",
                Year = 2022,
                Platform = PlatformFamily.NintendoSwitch
            };

            var gameFile = new GameFile
            {
                GameId = 8,
                ComponentId = 31,
                Quality = new QualityModel(Quality.Retail)
            };

            Mocker.GetMock<IGameComponentRepository>()
                  .Setup(x => x.Get(31))
                  .Returns(new GameComponent
                  {
                      Id = 31,
                      GameId = 8,
                      ComponentType = GameComponentType.Dlc,
                      Key = "steam:1",
                      Title = "Cultist Pack"
                  });

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.FindDlcByTitles(It.Is<System.Collections.Generic.IEnumerable<string>>(titles => titles.Contains("Cultist Pack"))))
                  .Returns(new SwitchTitleDbMatch("Cultist Pack", "01002E7016C47001"));

            Mocker.GetMock<ISwitchTitleDbService>()
                  .Setup(x => x.GetLatestVersion("01002E7016C47001"))
                  .Returns(0);

            Subject.BuildFileName(game, gameFile)
                   .Should().Be("Cult of the Lamb [01002E7016C47001][v0]");
        }
    }
}
