<?php
// Tim Davis #1332245
// Project Assignment #1
// index.php

require_once('PlayerDatabase.php');
require_once('Player.php');

// Don't do any database or logic work if there's no user input
if (isset($_GET["name"]) && $_GET["name"] != "") {
	$name = $_GET["name"];
	$data = get_data($name);

	// Create an array of all found players
	$resultCount = count($data);
	$players = array($resultCount);
	$i = 0;
	foreach ($data as $row) {
		$pName = $row['PlayerName'];
		$pGP = $row['GP'];
		$pFGP = $row['FGP'];
		$pTPP = $row['TPP'];
		$pFTP = $row['FTP'];
		$pPPG = $row['PPG'];
		
		$player = new Player($pName, $pGP, $pFGP, $pTPP, $pFTP, $pPPG);
		
		$players[$i] = $player;
		$i = $i + 1;
	}
}
?>

<!DOCTYPE html>
<html>
	<head>
		<title>NBA Player Search</title>
		<link href="index.css" type="text/css" rel="stylesheet" />
	</head>
	<body>
		<h1>Search for NBA Players</h1>
		<form action="index.php" method="get">
			<div>
				<input type="text" name="name" placeholder="player name" />
				<input type="submit" value="Search" />
			</div>
		</form>
		
		<?php
		if(isset($players) && count($players)) {
			echo("<h2>Results (" . $resultCount . " found)</h2>");
			foreach($players as $player) {
				$name = $player->GetName();
				$stats = $player->GetStats();
				$url = $player->GetUrl();
			?>
				<div class="player">
			<?php
				// player image
				echo '<img src="'.$url.'" alt="Player Image">';
				?>
					<div class="info">
					<?php
					// player name
					echo('<p class="name">' . $name . '</p>');
					// player stats
					echo('<p class="stats">Season Stats<br />GP: ' . $stats['GP'] . '<br />FGP: ' . $stats['FGP']
							. '<br />TPP: ' . $stats['TPP'] . '<br />FTP: ' . $stats['FTP'] . '<br />PPG: ' . $stats['PPG'] . '</p>');
					?>
					</div>
				</div>
			<?php
			}
		}
		?>
		<p id="disclaimer">Disclaimer: If a player image isn't found, then the player is most likely no longer in the NBA.</p>
	</body>
</html>
