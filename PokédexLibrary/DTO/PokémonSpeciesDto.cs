﻿using PokédexLibrary.DTO;

namespace PokédexLibrary.DTO
{
    public class PokémonSpeciesDto
    {
        public EvolutionChainDto evolution_chain { get; set; }
        public FlavorTextDto[] flavor_text_entries { get; set; }
    }
}
