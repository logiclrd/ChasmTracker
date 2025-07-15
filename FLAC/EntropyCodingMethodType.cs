namespace libFLAC;

/** An enumeration of the available entropy coding methods. */
public enum EntropyCodingMethodType
{
	PartitionedRice = 0,
	/**< Residual is coded by partitioning into contexts, each with it's own
	 * 4-bit Rice parameter. */

	PartitionedRice2 = 1,
	/**< Residual is coded by partitioning into contexts, each with it's own
	 * 5-bit Rice parameter. */
}
