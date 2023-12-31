﻿using Newtonsoft.Json;
using PokédexLibrary;
using PokédexLibrary.DTO;
using PokédexLibrary.Exceptions;
using PokédexLibrary.Extensions;
using PokédexLibrary.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace PokédexGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Pokédex pokédex;
        private Pokémon currentPokémon;
        private Team currentTeam;
        private readonly WebClient pokemonClient, pokemonSpeciesClient;
        private readonly PokéCache pokéCache;
        public MainWindow()
        {
            pokéCache = PokéCache.Instance;
            pokédex = new Pokédex();
            pokédex.Teams.Add(new Team("The very best like no one ever was"));
            currentTeam = pokédex.Teams.First();
            pokemonClient = new WebClient
            {
                BaseAddress = PokéCache.ApiUrl + "pokemon/"
            };
            pokemonSpeciesClient = new WebClient
            {
                BaseAddress = PokéCache.ApiUrl + "pokemon-species/"
            };

            InitializeComponent();
            GrpPkmn.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            pokemonClient.Dispose();
            pokemonSpeciesClient.Dispose();
            base.OnClosed(e);
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            var invoer = TxtSearch.Text;
            if (string.IsNullOrWhiteSpace(invoer))
            {
                MessageBox.Show("PLease enter a valid name or number of a Pokémon...", "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtSearch.Focus();
            }
            else
            {
                PokémonDto pkmnDto;
                PokémonSpeciesDto speciesDto;
                GUIEnabled(false);

                // check of pokémoninfo bestaat in cache
                if (int.TryParse(invoer, out int pokéId))
                {
                    // gebruiker heeft nummer ingevoerd
                    pkmnDto = pokéCache.PokémonDtos.Where(x => x?.id == pokéId).FirstOrDefault();
                }
                else
                {
                    // gebruiker heeft naam ingevoerd
                    pkmnDto = pokéCache.PokémonDtos.Where(x => x?.name == invoer.ToLower()).FirstOrDefault();
                }

                // cache hit
                if (pkmnDto != null)
                {
                    speciesDto = pokéCache.PokémonSpeciesDtos[pkmnDto.id];
                }
                else // cache miss, info downloaden...
                {
                    // download info van API
                    var pkmnTask = Task.Run(() => pokemonClient.DownloadString(invoer));
                    var speciesTask = Task.Run(() => pokemonSpeciesClient.DownloadString(invoer));
                    await Task.WhenAll(pkmnTask, speciesTask);

                    // deserialiseren van JSON string naar DTO object
                    pkmnDto = JsonConvert.DeserializeObject<PokémonDto>(pkmnTask.Result);
                    speciesDto = JsonConvert.DeserializeObject<PokémonSpeciesDto>(speciesTask.Result);

                    // DTO opslaan in cache voor hergebruik
                    pokéCache.PokémonDtos[pkmnDto.id] = pkmnDto;
                    pokéCache.PokémonSpeciesDtos[pkmnDto.id] = speciesDto;
                }

                TextInfo ti = CultureInfo.CurrentCulture.TextInfo;

                currentPokémon = new Pokémon()
                {
                    Id = pkmnDto.id,
                    Name = ti.ToTitleCase(pkmnDto.name),
                    Attack = pkmnDto.stats?.Where(stat => stat.stat.name == "attack").Select(stat => stat.base_stat).FirstOrDefault() ?? -1,
                    Defense = pkmnDto.stats?.Where(stat => stat.stat.name == "defense").Select(stat => stat.base_stat).FirstOrDefault() ?? -1,
                    HP = pkmnDto.stats?.Where(stat => stat.stat.name == "hp").Select(stat => stat.base_stat).FirstOrDefault() ?? -1,
                    Types = pkmnDto.types?.Select(type => type.type.name).ToList(),
                    Description = speciesDto.flavor_text_entries.Where(x => x.language.name == "en").Select(x => x.flavor_text.Unescape()).FirstOrDefault(),
                    ImageUrl = pkmnDto.sprites.front_default
                };
                UpdateGUI();
                GUIEnabled(true);
            }
        }

        private void UpdateGUI()
        {
            GrpPkmn.Header = currentPokémon.Name;
            
            if(currentPokémon.ImageUrl != null)
                ImgPkmn.Source = new BitmapImage(new Uri(currentPokémon.ImageUrl));

            LblAttack.Content = currentPokémon.Attack == -1 ? "N/A" : currentPokémon.Attack.ToString();
            LblDefense.Content = currentPokémon.Defense == -1 ? "N/A" : currentPokémon.Defense.ToString();
            LblHP.Content = currentPokémon.HP == -1 ? "N/A" : currentPokémon.HP.ToString();

            if(currentPokémon.Types != null)
            {
                LblTypes.Content = string.Join(", ", currentPokémon.Types);
            } else
            {
                LblTypes.Content = "N/A";
            }

            TxtDescription.Text = currentPokémon.Description ?? "No description available.";
        }

        private void UpdateMenu()
        {
            MenuPkmn.Items.Clear();
            foreach (var pkmn in currentTeam.AllPokémon)
            {
                MenuItem menuItem = new MenuItem();
                menuItem.Header = pkmn.Name;
                menuItem.Click += MenuPokémonClick;
                MenuPkmn.Items.Add(menuItem);
            }
        }

        private void MenuPokémonClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            /* volgende lijn code gebruikt een null-coalescing assignment operator (??=) en een null-conditional operator (?.)
             * Meer info hierover:
             * https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-coalescing-operator
             * https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and%2D
             */
            currentPokémon ??= currentTeam.AllPokémon.Find(x => x.Name == menuItem?.Name.ToString());
            UpdateGUI();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentTeam.AddPokémon(currentPokémon))
                {
                    UpdateMenu();
                }
                else
                {
                    MessageBox.Show($"{currentPokémon.Name} is already part of this team.", "Pokémon not added", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (TeamIsFullException ex)
            {
                MessageBox.Show(ex.Message, "Pokémon not added", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (currentTeam.AllPokémon.Contains(currentPokémon))
            {
                var result = MessageBox.Show($"Are you sure you want to remove {currentPokémon.Name} from your team?", "Remove Pokémon", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    currentTeam.RemovePokémon(currentPokémon);
                    UpdateMenu();
                } 
            }
            else
            {
                MessageBox.Show($"{currentPokémon.Name} is not on the current team.{Environment.NewLine}You can add them using the \"Add to team\" button.", "Pokémon not removed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GUIEnabled(bool isEnabled)
        {
            TxtSearch.IsEnabled = isEnabled;
            BtnSearch.IsEnabled = isEnabled;
            GrpPkmn.Visibility = isEnabled ? Visibility.Visible : Visibility.Hidden;
            BtnAdd.IsEnabled = isEnabled;
            BtnRemove.IsEnabled = isEnabled;
        }
    }
}
