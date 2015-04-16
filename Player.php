<?php
// Tim Davis #1332245
// Project Assignment #1
// Player.php

// Player class
class Player {
	private $name;
	// Player stats
	private $GP;
	private $FGP;
	private $TPP;
	private $FTP;
	private $PPG;
	// Player Image
	private $url = "http://i.cdn.turner.com/nba/nba/.element/img/2.0/sect/statscube/players/large/";
	
	public function __construct($pName, $pGP, $pFGP, $pTPP, $pFTP, $pPPG) {
		$this->name = $pName;
		$this->GP = $pGP;
		$this->FGP = $pFGP;
		$this->TPP = $pTPP;
		$this->FTP = $pFTP;
		$this->PPG = $pPPG;
		$this->url = $this->url . str_replace(" ", "_", $pName) . ".png";
	}
	
	// returns player name
	public function GetName() {
		return $this->name;
	}
	
	// returns all player stats in an array
	public function GetStats() {
		$stats = array("GP" => $this->GP, "FGP" => $this->FGP, "TPP" => $this->TPP, 
							"FTP" => $this->FTP, "PPG" => $this->PPG);
		return $stats;
	}
	
	// returns player url
	public function GetUrl() {
		return $this->url;
	}
}
?>
